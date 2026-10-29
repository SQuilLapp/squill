using Microsoft.Data.Sqlite;
using Squill.Core;

namespace Squill.Data.Sqlite;

/// <summary>Streams the rows of one executed SQLite command in batches.</summary>
internal sealed class SqliteResultReader : IResultReader
{
    private readonly SqliteCommand _command;
    private readonly SqliteDataReader _reader;
    private bool _finished;

    /// <summary>Takes ownership of an executed command and its open reader, and captures the column metadata.</summary>
    /// <param name="command">Command that produced <paramref name="reader"/>; disposed with this instance.</param>
    /// <param name="reader">Open reader positioned before the first row.</param>
    public SqliteResultReader(SqliteCommand command, SqliteDataReader reader)
    {
        _command = command;
        _reader = reader;
        var columns = new ColumnInfo[reader.FieldCount];
        for (var i = 0; i < columns.Length; i++)
            columns[i] = new ColumnInfo(reader.GetName(i), reader.GetDataTypeName(i), reader.GetFieldType(i), true, i);
        Columns = columns;
    }

    /// <inheritdoc />
    public IReadOnlyList<ColumnInfo> Columns { get; }

    /// <inheritdoc />
    public long RecordsAffected => _finished ? _reader.RecordsAffected : -1;

    /// <inheritdoc />
    public async ValueTask<object?[][]?> ReadBatchAsync(int maxRows, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxRows);
        var rows = new List<object?[]>();
        while (rows.Count < maxRows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_finished || !await _reader.ReadAsync(cancellationToken))
            {
                _finished = true;
                break;
            }
            var row = new object?[Columns.Count];
            for (var i = 0; i < row.Length; i++)
                row[i] = _reader.IsDBNull(i) ? null : _reader.GetValue(i);
            rows.Add(row);
        }
        return rows.Count == 0 ? null : [.. rows];
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _reader.DisposeAsync();
        await _command.DisposeAsync();
    }
}
