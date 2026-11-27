using Squill.Core;

namespace Squill.Data.Oracle;

/// <summary>Streams the rows of one statement run by the bridge, fetching batches on demand and moving across its result sets.</summary>
internal sealed class OracleResultReader : IResultReader
{
    private readonly BridgeClient _client;
    private readonly List<string> _messages = [];
    private bool _setDone;
    private bool _released;
    private object?[]? _outRow;

    /// <summary>Wraps the response of an <c>execute</c> request.</summary>
    /// <param name="client">The bridge that holds the open reader on its side.</param>
    /// <param name="executed">The response: columns, and whether the bridge already read everything.</param>
    /// <param name="outs">The output parameters the request declared; their values come back as a one-row result set named after them.</param>
    public OracleResultReader(BridgeClient client, BridgeResponse executed, WireOut[]? outs = null)
    {
        _client = client;
        _setDone = _released = executed.Done;
        RecordsAffected = executed.Done ? executed.RecordsAffected : -1;
        Columns = MapColumns(executed);
        AddMessages(executed);
        if (outs is { Length: > 0 } && executed.OutValues is { } values)
        {
            Columns = [.. outs.Select((o, i) => new ColumnInfo(o.Name, o.Kind == "d" ? "NUMBER" : o.Kind == "x" ? "RAW" : "VARCHAR2", WireValue.ClrType(o.Kind), true, i))];
            _outRow = [.. values.Select(WireValue.Decode)];
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ColumnInfo> Columns { get; private set; }

    /// <inheritdoc />
    public long RecordsAffected { get; private set; }

    /// <inheritdoc />
    public IReadOnlyList<string> Messages => _messages;

    private static ColumnInfo[] MapColumns(BridgeResponse response) =>
        [.. (response.Columns ?? []).Select((c, i) => new ColumnInfo(c.Name, c.DataType, WireValue.ClrType(c.Kind), true, i))];

    private void AddMessages(BridgeResponse response)
    {
        if (response.Messages is { } lines)
            _messages.AddRange(lines);
    }

    /// <inheritdoc />
    public async ValueTask<object?[][]?> ReadBatchAsync(int maxRows, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxRows);
        if (_outRow is { } outRow)
        {
            _outRow = null;
            return [outRow];
        }
        if (_setDone)
            return null;
        var response = await _client.SendAsync(new BridgeRequest(0, "fetch", MaxRows: maxRows), cancellationToken);
        AddMessages(response);
        if (response.Done)
        {
            _setDone = true;
            RecordsAffected = response.RecordsAffected;
        }
        var rows = response.Rows ?? [];
        return rows.Length == 0 ? null : [.. rows.Select(r => r.Select(WireValue.Decode).ToArray())];
    }

    /// <inheritdoc />
    public async ValueTask<bool> NextResultAsync(CancellationToken cancellationToken)
    {
        if (_released)
            return false;
        var response = await _client.SendAsync(new BridgeRequest(0, "nextResult"), cancellationToken);
        AddMessages(response);
        if (response.Done)
        {
            _setDone = _released = true;
            Columns = [];
            if (response.RecordsAffected >= 0)
                RecordsAffected = response.RecordsAffected;
            return false;
        }
        _setDone = false;
        Columns = MapColumns(response);
        return true;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_released)
            return;
        _setDone = _released = true;
        try
        {
            await _client.SendAsync(new BridgeRequest(0, "closeReader"), CancellationToken.None);
        }
        catch (OracleBridgeException)
        {
            // The bridge died: nothing is left to release.
        }
    }
}
