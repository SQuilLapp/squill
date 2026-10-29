using Microsoft.Data.Sqlite;
using Squill.Core;

namespace Squill.Data.Sqlite;

internal sealed class SqliteResultReader : IResultReader
{
    private readonly SqliteCommand _command;
    private readonly SqliteDataReader _reader;
    private bool _finished;

    public SqliteResultReader(SqliteCommand command, SqliteDataReader reader)
    {
        _command = command;
        _reader = reader;
        var columns = new ColumnInfo[reader.FieldCount];
        for (var i = 0; i < columns.Length; i++)
            columns[i] = new ColumnInfo(reader.GetName(i), reader.GetDataTypeName(i), reader.GetFieldType(i), true, i);
        Columns = columns;
    }

    public IReadOnlyList<ColumnInfo> Columns { get; }

    public long RecordsAffected => _finished ? _reader.RecordsAffected : -1;

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

    public async ValueTask DisposeAsync()
    {
        await _reader.DisposeAsync();
        await _command.DisposeAsync();
    }
}
