using System.IO.Pipelines;
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

    /// <summary>Starts the fake; a handler returning <c>null</c> never answers (a hung statement).</summary>
    public FakeBridge(Func<BridgeRequest, BridgeResponse?> handler)
    {
        Client = new BridgeClient(_toBridge.Writer.AsStream(), _fromBridge.Reader.AsStream(), graceful =>
        {
            if (!graceful)
                Killed = true;
            _stop.Cancel();
            _fromBridge.Writer.Complete();
        });
        _loop = Task.Run(async () =>
        {
            var utf8 = new UTF8Encoding(false);
            using var input = new StreamReader(_toBridge.Reader.AsStream(), utf8);
            await using var output = new StreamWriter(_fromBridge.Writer.AsStream(), utf8) { AutoFlush = true, NewLine = "\n" };
            try
            {
                while (await input.ReadLineAsync(_stop.Token) is { } line)
                {
                    var request = JsonSerializer.Deserialize(line, BridgeJsonContext.Default.BridgeRequest)!;
                    lock (Requests)
                        Requests.Add(request);
                    if (handler(request) is not { } response)
                        await Task.Delay(Timeout.Infinite, _stop.Token);
                    else
                        await output.WriteLineAsync(JsonSerializer.Serialize(response with { Id = request.Id }, BridgeJsonContext.Default.BridgeResponse));
                    if (request.Op == "close")
                        return;
                }
            }
            catch (OperationCanceledException)
            {
                // Killed by the client.
            }
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
