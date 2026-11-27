using System.IO.Pipelines;
using System.Threading.Channels;
using System.Text;
using System.Text.Json;

namespace Squill.Data.Oracle.Tests;

/// <summary>An in-process stand-in for the bridge process: answers each request line through a handler, over a pair of pipes.</summary>
internal sealed class FakeBridge : IAsyncDisposable
{
    private readonly Pipe _toBridge = new();
    private readonly Pipe _fromBridge = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _loop;

    /// <summary>
    /// Starts the fake; a handler returning <c>null</c> never answers (a hung statement) unless <paramref name="honourCancel"/> is set, in which case a
    /// <c>cancel</c> line makes it answer <c>ORA-01013</c> and keeps the bridge alive (as the real one does).
    /// </summary>
    public FakeBridge(Func<BridgeRequest, BridgeResponse?> handler, bool honourCancel = false)
    {
        Client = new BridgeClient(_toBridge.Writer.AsStream(), _fromBridge.Reader.AsStream(), graceful =>
        {
            if (!graceful)
                Killed = true;
            _stop.Cancel();
            _fromBridge.Writer.Complete();
        }, TimeSpan.FromMilliseconds(300));
        var utf8 = new UTF8Encoding(false);
        var output = new StreamWriter(_fromBridge.Writer.AsStream(), utf8) { AutoFlush = true, NewLine = "\n" };
        var outputGate = new SemaphoreSlim(1, 1);
        var queue = Channel.CreateUnbounded<BridgeRequest>();
        var cancelled = new SemaphoreSlim(0);

        async Task WriteAsync(BridgeResponse response)
        {
            await outputGate.WaitAsync(_stop.Token);
            try
            {
                await output.WriteLineAsync(JsonSerializer.Serialize(response, BridgeJsonContext.Default.BridgeResponse));
            }
            finally
            {
                outputGate.Release();
            }
        }

        var worker = Task.Run(async () =>
        {
            try
            {
                await foreach (var request in queue.Reader.ReadAllAsync(_stop.Token))
                {
                    if (handler(request) is { } response)
                        await WriteAsync(response with { Id = request.Id });
                    else if (honourCancel)
                    {
                        await cancelled.WaitAsync(_stop.Token);
                        await WriteAsync(new BridgeResponse(request.Id, false, "ORA-01013: user requested cancel of current operation"));
                    }
                    else
                        await Task.Delay(Timeout.Infinite, _stop.Token);
                    if (request.Op == "close")
                        return;
                }
            }
            catch (OperationCanceledException)
            {
                // Killed by the client.
            }
        });
        _loop = Task.Run(async () =>
        {
            using var input = new StreamReader(_toBridge.Reader.AsStream(), utf8);
            try
            {
                while (await input.ReadLineAsync(_stop.Token) is { } line)
                {
                    var request = JsonSerializer.Deserialize(line, BridgeJsonContext.Default.BridgeRequest)!;
                    lock (Requests)
                        Requests.Add(request);
                    if (request.Op == "cancel")
                    {
                        cancelled.Release();
                        await WriteAsync(new BridgeResponse(request.Id, true));
                    }
                    else
                        await queue.Writer.WriteAsync(request, _stop.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // Killed by the client.
            }
            await worker;
        });
    }

    /// <summary>The client side, to hand to a provider or a connection.</summary>
    public BridgeClient Client { get; }

    /// <summary>Whether the client killed the bridge instead of closing it.</summary>
    public bool Killed { get; private set; }

    /// <summary>Every request received, in order.</summary>
    public List<BridgeRequest> Requests { get; } = [];

    /// <summary>Operations received, in order.</summary>
    public string[] Ops
    {
        get
        {
            lock (Requests)
                return [.. Requests.Select(r => r.Op)];
        }
    }

    /// <summary>A handler that serves canned result sets: <c>execute</c> picks the first set whose key the SQL contains, <c>fetch</c> pages through it.</summary>
    public static Func<BridgeRequest, BridgeResponse?> Scripted(Dictionary<string, (WireColumn[] Columns, WireCell?[][] Rows)> sets, string version = "Oracle Database 23ai")
    {
        (WireColumn[] Columns, WireCell?[][] Rows) current = ([], []);
        var next = 0;
        return request =>
        {
            switch (request.Op)
            {
                case "open":
                    return new BridgeResponse(0, true, ServerVersion: version);
                case "execute":
                    var match = sets.Where(s => request.Sql!.Contains(s.Key, StringComparison.OrdinalIgnoreCase)).Select(s => s.Value).DefaultIfEmpty().First();
                    if (match.Columns is null)
                        return new BridgeResponse(0, false, "ORA-00942: table or view does not exist");
                    current = match;
                    next = 0;
                    return new BridgeResponse(0, true, Columns: current.Columns, Done: current.Columns.Length == 0, RecordsAffected: current.Rows.Length);
                case "nextResult":
                    return new BridgeResponse(0, true, Done: true);
                case "fetch":
                    var rows = current.Rows.Skip(next).Take(request.MaxRows!.Value).ToArray();
                    next += rows.Length;
                    return new BridgeResponse(0, true, Rows: rows, Done: next >= current.Rows.Length, RecordsAffected: next);
                default:
                    return new BridgeResponse(0, true);
            }
        };
    }

    /// <summary>Stops the loop.</summary>
    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        await _loop;
        _stop.Dispose();
    }
}
