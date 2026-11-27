using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Squill.Data.Oracle;

/// <summary>
/// Talks to one bridge process over two streams: one JSON line per request, one JSON line per response, strictly in turn. The only line sent
/// out of turn is <c>cancel</c> (negative id, answered at once and skipped by the reader loop).
/// </summary>
internal sealed class BridgeClient : IAsyncDisposable
{
    /// <summary>Environment variable that overrides the bridge location (an executable, or a <c>.dll</c> started with <c>dotnet</c>).</summary>
    public const string PathVariable = "SQUILL_ORACLE_BRIDGE";

    private static readonly UTF8Encoding Utf8 = new(false);
    private readonly StreamWriter _writer;
    private readonly StreamReader _reader;
    private readonly Action<bool> _stop;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly TimeSpan _cancelGrace;
    private long _nextId;
    private long _cancelId;
    private long _active;
    private volatile bool _broken;

    /// <summary>Wraps the two ends of a bridge.</summary>
    /// <param name="toBridge">The bridge's stdin.</param>
    /// <param name="fromBridge">The bridge's stdout.</param>
    /// <param name="stop">Stops the bridge: with <c>true</c> waits a moment for a clean exit first, with <c>false</c> kills it at once; idempotent.</param>
    /// <param name="cancelGrace">How long a cancelled request may take to answer before the bridge is killed; defaults to 5 seconds.</param>
    public BridgeClient(Stream toBridge, Stream fromBridge, Action<bool> stop, TimeSpan? cancelGrace = null)
    {
        _cancelGrace = cancelGrace ?? TimeSpan.FromSeconds(5);
        _writer = new StreamWriter(toBridge, Utf8) { NewLine = "\n" };
        _reader = new StreamReader(fromBridge, Utf8);
        _stop = stop;
    }

    /// <summary>Starts the bridge process found by <see cref="ResolvePath"/>.</summary>
    public static BridgeClient StartProcess()
    {
        var path = ResolvePath();
        var info = new ProcessStartInfo(path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? "dotnet" : path)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        if (info.FileName == "dotnet")
            info.ArgumentList.Add(path);
        var process = Process.Start(info) ?? throw new OracleBridgeException("The Oracle bridge process did not start.");
        return new BridgeClient(process.StandardInput.BaseStream, process.StandardOutput.BaseStream, graceful =>
        {
            try
            {
                if (graceful && process.WaitForExit(2000))
                    return;
                if (!process.HasExited)
                    process.Kill(true);
            }
            catch (InvalidOperationException)
            {
                // Already gone.
            }
        });
    }

    /// <summary>Finds the bridge: the <see cref="PathVariable"/> variable, else <c>oracle-bridge/</c> next to the application.</summary>
    /// <exception cref="FileNotFoundException">No bridge is installed.</exception>
    public static string ResolvePath()
    {
        var path = Environment.GetEnvironmentVariable(PathVariable);
        if (string.IsNullOrEmpty(path))
            path = Path.Combine(AppContext.BaseDirectory, "oracle-bridge", OperatingSystem.IsWindows() ? "Squill.OracleBridge.exe" : "Squill.OracleBridge");
        return File.Exists(path)
            ? path
            : throw new FileNotFoundException($"The Oracle bridge was not found at '{path}'. Publish src/Squill.OracleBridge there or set {PathVariable}.", path);
    }

    /// <summary>Sends a request and waits for its response.</summary>
    /// <param name="request">The request; its <see cref="BridgeRequest.Id"/> is replaced.</param>
    /// <param name="cancellationToken">
    /// Cancelling sends <c>cancel</c> to the bridge, which interrupts the statement (<c>ORA-01013</c>) and keeps the connection usable; only a bridge that
    /// does not answer within the grace period is killed.
    /// </param>
    /// <returns>The successful response.</returns>
    /// <exception cref="OracleBridgeException">The bridge reported an error, died or answered out of turn.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled and the request did not complete.</exception>
    public async ValueTask<BridgeResponse> SendAsync(BridgeRequest request, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_broken)
                throw new OracleBridgeException("The Oracle bridge is no longer running.");
            cancellationToken.ThrowIfCancellationRequested();
            request = request with { Id = ++_nextId };
            Volatile.Write(ref _active, request.Id);
            using var abort = cancellationToken.Register(static state => ((BridgeClient)state!).OnCancel(), this);
            BridgeResponse? response;
            try
            {
                await WriteLineAsync(JsonSerializer.Serialize(request, BridgeJsonContext.Default.BridgeRequest));
                response = await ReadResponseAsync();
            }
            catch (Exception e) when (e is IOException or ObjectDisposedException)
            {
                Break();
                cancellationToken.ThrowIfCancellationRequested();
                throw new OracleBridgeException("The Oracle bridge connection failed.", e);
            }
            if (response is null)
            {
                Break();
                cancellationToken.ThrowIfCancellationRequested();
                throw new OracleBridgeException("The Oracle bridge exited unexpectedly.");
            }
            if (response.Id != request.Id)
            {
                Break();
                throw new OracleBridgeException($"The Oracle bridge answered request {response.Id} instead of {request.Id}.");
            }
            if (response.Ok)
                return response;
            cancellationToken.ThrowIfCancellationRequested();
            throw new OracleBridgeException(response.Error ?? "The Oracle bridge reported an unknown error.");
        }
        finally
        {
            Volatile.Write(ref _active, 0);
            _gate.Release();
        }
    }

    // Reads the next response that is not the answer to a cancel (those have negative ids and may arrive before or after the real answer).
    private async Task<BridgeResponse?> ReadResponseAsync()
    {
        while (await _reader.ReadLineAsync() is { } line)
        {
            var response = JsonSerializer.Deserialize(line, BridgeJsonContext.Default.BridgeResponse)
                ?? throw new OracleBridgeException("The Oracle bridge sent an empty response.");
            if (response.Id >= 0)
                return response;
        }
        return null;
    }

    private async Task WriteLineAsync(string json)
    {
        await _writeGate.WaitAsync();
        try
        {
            await _writer.WriteLineAsync(json.AsMemory());
            await _writer.FlushAsync();
        }
        finally
        {
            _writeGate.Release();
        }
    }

    // Runs on the cancelling thread: asks the bridge to interrupt the statement, and kills it only if the request is still unanswered after the grace period.
    private void OnCancel()
    {
        var id = Volatile.Read(ref _active);
        if (_broken || id == 0)
            return;
        _ = Task.Run(async () =>
        {
            try
            {
                var cancel = new BridgeRequest(-Interlocked.Increment(ref _cancelId), "cancel");
                await WriteLineAsync(JsonSerializer.Serialize(cancel, BridgeJsonContext.Default.BridgeRequest));
                await Task.Delay(_cancelGrace);
            }
            catch (Exception e) when (e is IOException or ObjectDisposedException)
            {
                // The pipe is gone: the request fails on its own.
            }
            if (Volatile.Read(ref _active) == id)
                Break();
        });
    }

    private void Break()
    {
        _broken = true;
        _stop(false);
    }

    /// <summary>Asks the bridge to close, then makes sure the process is gone.</summary>
    public async ValueTask DisposeAsync()
    {
        if (!_broken)
        {
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await SendAsync(new BridgeRequest(0, "close"), timeout.Token);
            }
            catch (Exception e) when (e is OracleBridgeException or OperationCanceledException)
            {
                // Best effort: the process is stopped below.
            }
        }
        _broken = true;
        _stop(true);
    }
}
