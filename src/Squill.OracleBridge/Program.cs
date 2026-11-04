using System.Text;
using System.Text.Json;
using Squill.Data.Oracle;

// Protocol: one JSON request per stdin line, one JSON response per stdout line (see docs/poc/oracle-bridge.md). Nothing else may write to stdout.
var utf8 = new UTF8Encoding(false);
using var input = new StreamReader(Console.OpenStandardInput(), utf8);
using var output = new StreamWriter(Console.OpenStandardOutput(), utf8) { AutoFlush = true, NewLine = "\n" };
await using var session = new OracleSession();

while (await input.ReadLineAsync() is { } line)
{
    var request = JsonSerializer.Deserialize(line, BridgeJsonContext.Default.BridgeRequest);
    if (request is null)
        continue;
    var response = await session.HandleAsync(request);
    await output.WriteLineAsync(JsonSerializer.Serialize(response, BridgeJsonContext.Default.BridgeResponse));
    if (request.Op == "close")
        break;
}
