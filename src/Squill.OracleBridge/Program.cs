using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Squill.Data.Oracle;
using Squill.OracleBridge;

// Protocol: one JSON request per stdin line, one JSON response per stdout line (see docs/poc/oracle-bridge.md). Nothing else may write to stdout.
// This thread only reads: `cancel` is answered here at once (it interrupts the statement a worker is running), every other request is queued for the worker.
var utf8 = new UTF8Encoding(false);
using var input = new StreamReader(Console.OpenStandardInput(), utf8);
using var output = new StreamWriter(Console.OpenStandardOutput(), utf8) { AutoFlush = true, NewLine = "\n" };
using var outputGate = new SemaphoreSlim(1, 1);
await using var session = new OracleSession();
var queue = Channel.CreateUnbounded<BridgeRequest>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });

async Task WriteAsync(BridgeResponse response)
{
    await outputGate.WaitAsync();
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
    await foreach (var request in queue.Reader.ReadAllAsync())
    {
        await WriteAsync(await session.HandleAsync(request));
        if (request.Op == "close")
            break;
    }
});

while (await input.ReadLineAsync() is { } line)
{
    var request = JsonSerializer.Deserialize(line, BridgeJsonContext.Default.BridgeRequest);
    if (request is null)
        continue;
    if (request.Op == "cancel")
    {
        session.Cancel();
        await WriteAsync(new BridgeResponse(request.Id, true));
        continue;
    }
    await queue.Writer.WriteAsync(request);
    if (request.Op == "close")
        break;
}

queue.Writer.Complete();
await worker;
