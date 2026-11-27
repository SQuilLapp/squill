using Squill.Core;

namespace Squill.Data.Informix;

/// <summary>Streams the rows of one Informix statement in batches over an <see cref="OdbcReader"/>.</summary>
internal sealed class InformixResultReader : IResultReader
{
    private readonly OdbcReader _reader;
    private long _affected;
    private bool _ended;
    private bool _counted;

    /// <summary>Takes ownership of an executed reader, positioned on its first result set.</summary>
    /// <param name="reader">The reader; disposed with this instance.</param>
    public InformixResultReader(OdbcReader reader)
    {
        _reader = reader;
        Columns = MapColumns(reader);
    }

    /// <inheritdoc />
    public IReadOnlyList<ColumnInfo> Columns { get; private set; }

    /// <inheritdoc />
    public long RecordsAffected => _ended ? _affected : -1;

    private static ColumnInfo[] MapColumns(OdbcReader reader)
    {
        var columns = new ColumnInfo[reader.FieldCount];
        for (var i = 0; i < columns.Length; i++)
        {
            var (name, clr) = OdbcValue.Describe(reader.ColumnSqlType(i));
            columns[i] = new ColumnInfo(reader.ColumnName(i), name, clr, true, i);
        }

        return columns;
    }

    /// <inheritdoc />
    public async ValueTask<object?[][]?> ReadBatchAsync(int maxRows, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxRows);
        var rows = new List<object?[]>();
        try
        {
            while (rows.Count < maxRows && await _reader.ReadAsync(cancellationToken))
                rows.Add([.. Enumerable.Range(0, _reader.FieldCount).Select(_reader.GetValue)]);
        }
        catch (OdbcException ex) when (cancellationToken.IsCancellationRequested)
        {
            // The driver reports a cancelled statement as a diagnostic; callers expect the standard cancellation exception.
            throw new OperationCanceledException(ex.Message, ex, cancellationToken);
        }

        if (rows.Count > 0)
            return [.. rows];
        CountCurrentSet();
        return null;
    }

    /// <inheritdoc />
    public async ValueTask<bool> NextResultAsync(CancellationToken cancellationToken)
    {
        CountCurrentSet();
        try
        {
            if (!await _reader.NextResultAsync(cancellationToken))
            {
                _ended = true;
                Columns = [];
                return false;
            }
        }
        catch (OdbcException ex) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(ex.Message, ex, cancellationToken);
        }

        _ended = _counted = false;
        Columns = MapColumns(_reader);
        return true;
    }

    // Only statements without a result set report a count (a SELECT would report the rows fetched so far).
    private void CountCurrentSet()
    {
        _ended = true;
        if (_reader.FieldCount == 0 && _reader.RowsAffected >= 0 && !_counted)
        {
            _affected += _reader.RowsAffected;
            _counted = true;
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _reader.Dispose();
        return ValueTask.CompletedTask;
    }
}
