using Squill.Core;

namespace Squill.Data.Oracle;

/// <summary>Streams the rows of one statement run by the bridge, fetching batches on demand and moving across its result sets.</summary>
internal sealed class OracleResultReader : IResultReader
{
    private readonly BridgeClient _client;
    private readonly List<string> _messages = [];
    private bool _setDone;
    private bool _released;

    /// <summary>Wraps the response of an <c>execute</c> request.</summary>
    /// <param name="client">The bridge that holds the open reader on its side.</param>
    /// <param name="executed">The response: columns, and whether the bridge already read everything.</param>
    public OracleResultReader(BridgeClient client, BridgeResponse executed)
    {
        _client = client;
        _setDone = _released = executed.Done;
        RecordsAffected = executed.Done ? executed.RecordsAffected : -1;
        Columns = MapColumns(executed);
        AddMessages(executed);
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
