using System.Text.Json.Serialization;

namespace Squill.Data.Oracle;

/// <summary>One typed value on the wire; a SQL NULL is a JSON <c>null</c> instead. Kinds: s string, i int64, d decimal, f double, b bool (1/0), t DateTime, o DateTimeOffset, x bytes (base64).</summary>
internal sealed record WireCell(string K, string V);

/// <summary>A named statement parameter sent to the bridge.</summary>
internal sealed record WireParameter(string Name, WireCell? Value);

/// <summary>An output parameter of an <c>execute</c> request (<c>RETURNING ... INTO :name</c>); <paramref name="Kind"/> is the wire kind its value comes back as (d, s or x).</summary>
internal sealed record WireOut(string Name, string Kind);

/// <summary>Column metadata of a result set; <paramref name="Kind"/> is the wire kind most values of the column use.</summary>
internal sealed record WireColumn(string Name, string DataType, string Kind);

/// <summary>
/// A request line. <c>open</c> uses the connection fields (<see cref="Descriptor"/>, when set, replaces <see cref="Host"/>, <see cref="Port"/> and <see cref="Service"/> as the data source, and <see cref="WalletPath"/> names the wallet folder), <c>execute</c> uses <see cref="Sql"/>, <see cref="Parameters"/> and optionally <see cref="OutParameters"/>, <c>fetch</c> uses
/// <see cref="MaxRows"/>, <c>savepoint</c> and <c>rollback</c> (to a savepoint) use <see cref="Name"/>; <c>begin</c>, <c>commit</c>, <c>nextResult</c>,
/// <c>probe</c>, <c>cancel</c>, <c>closeReader</c> and <c>close</c> need nothing more.
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
    int? MaxRows = null,
    string? Name = null,
    WireOut[]? OutParameters = null,
    string? Descriptor = null,
    string? WalletPath = null);

/// <summary>
/// A response line; <see cref="Id"/> echoes the request and <see cref="Error"/> is set when <see cref="Ok"/> is false. <see cref="Messages"/> carries the
/// <c>DBMS_OUTPUT</c> lines produced since the previous response; <see cref="InTransaction"/> answers <c>probe</c>; for <c>nextResult</c>,
/// <see cref="Done"/> means there is no further result set; <see cref="OutValues"/> holds the output parameters of an <c>execute</c> that declared some, in the order of <see cref="BridgeRequest.OutParameters"/>.
/// </summary>
internal sealed record BridgeResponse(
    long Id,
    bool Ok,
    string? Error = null,
    string? ServerVersion = null,
    WireColumn[]? Columns = null,
    WireCell?[][]? Rows = null,
    bool Done = false,
    long RecordsAffected = -1,
    string[]? Messages = null,
    bool? InTransaction = null,
    WireCell?[]? OutValues = null);

/// <summary>Source-generated JSON metadata of the bridge protocol (one JSON object per line).</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(BridgeRequest))]
[JsonSerializable(typeof(BridgeResponse))]
internal sealed partial class BridgeJsonContext : JsonSerializerContext;
