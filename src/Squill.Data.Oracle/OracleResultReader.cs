using Squill.Core;

namespace Squill.Data.Oracle;

/// <summary>Streams the rows of one statement run by the bridge, fetching batches on demand.</summary>
internal sealed class OracleResultReader : IResultReader
{
    private readonly BridgeClient _client;
    private bool _done;

    /// <summary>Wraps the response of an <c>execute</c> request.</summary>
    /// <param name="client">The bridge that holds the open reader on its side.</param>
    /// <param name="executed">The response: columns, and whether the bridge already read everything.</param>
    public OracleResultReader(BridgeClient client, BridgeResponse executed)
    {
        _client = client;
        _done = executed.Done;
        RecordsAffected = executed.Done ? executed.RecordsAffected : -1;
        Columns = [.. (executed.Columns ?? []).Select((c, i) => new ColumnInfo(c.Name, c.DataType, WireValue.ClrType(c.Kind), true, i))];
    }

    /// <inheritdoc />
    public IReadOnlyList<ColumnInfo> Columns { get; }

    /// <inheritdoc />
    public long RecordsAffected { get; private set; }

    /// <inheritdoc />
    public async ValueTask<object?[][]?> ReadBatchAsync(int maxRows, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxRows);
        if (_done)
            return null;
        var response = await _client.SendAsync(new BridgeRequest(0, "fetch", MaxRows: maxRows), cancellationToken);
        if (response.Done)
        {
            _done = true;
            RecordsAffected = response.RecordsAffected;
        }
        var rows = response.Rows ?? [];
        return rows.Length == 0 ? null : [.. rows.Select(r => r.Select(WireValue.Decode).ToArray())];
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_done)
            return;
        _done = true;
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
