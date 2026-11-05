using System.Text.Json.Serialization;

namespace Squill.Data.Oracle;

/// <summary>One typed value on the wire; a SQL NULL is a JSON <c>null</c> instead. Kinds: s string, i int64, d decimal, f double, b bool (1/0), t DateTime, o DateTimeOffset, x bytes (base64).</summary>
internal sealed record WireCell(string K, string V);

/// <summary>A named statement parameter sent to the bridge.</summary>
internal sealed record WireParameter(string Name, WireCell? Value);

/// <summary>Column metadata of a result set; <paramref name="Kind"/> is the wire kind most values of the column use.</summary>
internal sealed record WireColumn(string Name, string DataType, string Kind);

/// <summary>
/// A request line. <c>open</c> uses the connection fields, <c>execute</c> uses <see cref="Sql"/> and <see cref="Parameters"/>, <c>fetch</c> uses
/// <see cref="MaxRows"/>; <c>closeReader</c> and <c>close</c> need nothing more.
/// </summary>
internal sealed record BridgeRequest(
    long Id,
    string Op,
    string? Host = null,
    int? Port = null,
    string? Service = null,
    string? User = null,
    string? Password = null,
    int? ConnectTimeoutSeconds = null,
    int? CommandTimeoutSeconds = null,
    string? Sql = null,
    WireParameter[]? Parameters = null,
    int? MaxRows = null);

/// <summary>A response line; <see cref="Id"/> echoes the request and <see cref="Error"/> is set when <see cref="Ok"/> is false.</summary>
internal sealed record BridgeResponse(
    long Id,
    bool Ok,
    string? Error = null,
    string? ServerVersion = null,
    WireColumn[]? Columns = null,
    WireCell?[][]? Rows = null,
    bool Done = false,
    long RecordsAffected = -1);

/// <summary>Source-generated JSON metadata of the bridge protocol (one JSON object per line).</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(BridgeRequest))]
[JsonSerializable(typeof(BridgeResponse))]
internal sealed partial class BridgeJsonContext : JsonSerializerContext;
