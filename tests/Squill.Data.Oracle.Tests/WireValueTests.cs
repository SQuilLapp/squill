using System.Text.Json;
using Xunit;

namespace Squill.Data.Oracle.Tests;

public sealed class WireValueTests
{
    public static TheoryData<object> Values =>
    [
        "hello \n \"quoted\" é",
        "",
        42L,
        -7L,
        12345678901234567890.123456789m,
        1.5d,
        true,
        false,
        new DateTime(2026, 11, 4, 10, 30, 15, DateTimeKind.Unspecified).AddTicks(1234),
        new DateTimeOffset(2026, 11, 4, 10, 30, 15, TimeSpan.FromHours(2)),
        new byte[] { 0, 1, 2, 255 },
    ];

    [Theory]
    [MemberData(nameof(Values))]
    public void EncodeDecode_ThroughJson_RoundTrips(object value)
    {
        var request = new BridgeRequest(1, "execute", Parameters: [new WireParameter("p", WireValue.Encode(value))]);
        var line = JsonSerializer.Serialize(request, BridgeJsonContext.Default.BridgeRequest);

        Assert.DoesNotContain('\n', line);
        var back = JsonSerializer.Deserialize(line, BridgeJsonContext.Default.BridgeRequest)!;
        Assert.Equal(value, WireValue.Decode(back.Parameters![0].Value));
    }

    [Fact]
    public void Decode_Null_StaysNull() => Assert.Null(WireValue.Decode(WireValue.Encode(null)));

    [Fact]
    public void Encode_SmallIntegersAndDbNull_UseWireKinds()
    {
        Assert.Equal(new WireCell("i", "5"), WireValue.Encode(5));
        Assert.Null(WireValue.Encode(DBNull.Value));
        Assert.Equal(new WireCell("s", "a1b2"), WireValue.Encode("a1b2"));
    }

    [Fact]
    public void Decode_UnknownKind_Throws() =>
        Assert.Throws<FormatException>(() => WireValue.Decode(new WireCell("?", "x")));

    [Fact]
    public void Response_NullCellsAndDefaults_AreOmittedAndRestored()
    {
        var response = new BridgeResponse(3, true, Rows: [[new WireCell("i", "1"), null]], Done: true);
        var line = JsonSerializer.Serialize(response, BridgeJsonContext.Default.BridgeResponse);
        var back = JsonSerializer.Deserialize(line, BridgeJsonContext.Default.BridgeResponse)!;

        Assert.Equal(3, back.Id);
        Assert.Null(back.Error);
        Assert.Equal([new WireCell("i", "1"), null], back.Rows![0]);
        Assert.True(back.Done);
    }
}
