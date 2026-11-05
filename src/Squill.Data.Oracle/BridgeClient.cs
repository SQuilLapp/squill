using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Squill.Data.Oracle;

/// <summary>Talks to one bridge process over two streams: one JSON line per request, one JSON line per response, strictly in turn.</summary>
internal sealed class BridgeClient : IAsyncDisposable
{
    /// <summary>Environment variable that overrides the bridge location (an executable, or a <c>.dll</c> started with <c>dotnet</c>).</summary>
    public const string PathVariable = "SQUILL_ORACLE_BRIDGE";

    private static readonly UTF8Encoding Utf8 = new(false);
    private readonly StreamWriter _writer;
    private readonly StreamReader _reader;
    private readonly Action<bool> _stop;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private long _nextId;
    private volatile bool _broken;

    /// <summary>Wraps the two ends of a bridge.</summary>
    /// <param name="toBridge">The bridge's stdin.</param>
    /// <param name="fromBridge">The bridge's stdout.</param>
    /// <param name="stop">Stops the bridge: with <c>true</c> waits a moment for a clean exit first, with <c>false</c> kills it at once; idempotent.</param>
    public BridgeClient(Stream toBridge, Stream fromBridge, Action<bool> stop)
    {
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
    /// <param name="cancellationToken">Cancelling kills the bridge (a running statement cannot be interrupted); the client is then unusable.</param>
    /// <returns>The successful response.</returns>
    /// <exception cref="OracleBridgeException">The bridge reported an error, died or answered out of turn.</exception>
    public async ValueTask<BridgeResponse> SendAsync(BridgeRequest request, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_broken)
                throw new OracleBridgeException("The Oracle bridge is no longer running.");
            request = request with { Id = ++_nextId };
            using var abort = cancellationToken.Register(static state => ((BridgeClient)state!).Break(), this);
            string? line;
            try
            {
                await _writer.WriteLineAsync(JsonSerializer.Serialize(request, BridgeJsonContext.Default.BridgeRequest).AsMemory(), cancellationToken);
                await _writer.FlushAsync(cancellationToken);
                line = await _reader.ReadLineAsync(cancellationToken);
            }
            catch (Exception) when (cancellationToken.IsCancellationRequested)
            {
                Break();
                throw new OperationCanceledException(cancellationToken);
            }
            catch (Exception e) when (e is IOException or ObjectDisposedException)
            {
                Break();
                throw new OracleBridgeException("The Oracle bridge connection failed.", e);
            }
            if (line is null)
            {
                Break();
                throw new OracleBridgeException("The Oracle bridge exited unexpectedly.");
            }
            var response = JsonSerializer.Deserialize(line, BridgeJsonContext.Default.BridgeResponse)
                ?? throw new OracleBridgeException("The Oracle bridge sent an empty response.");
            if (response.Id != request.Id)
                throw new OracleBridgeException($"The Oracle bridge answered request {response.Id} instead of {request.Id}.");
            return response.Ok ? response : throw new OracleBridgeException(response.Error ?? "The Oracle bridge reported an unknown error.");
        }
        finally
        {
            _gate.Release();
        }
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
