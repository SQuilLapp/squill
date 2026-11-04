using Microsoft.Data.Sqlite;
using Squill.Core;

namespace Squill.Data.Sqlite;

/// <summary>Streams the rows of one executed SQLite command in batches.</summary>
internal sealed class SqliteResultReader : IResultReader
{
    private readonly SqliteConnection _connection;
    private readonly int _commandTimeoutSeconds;
    private readonly SqliteCommand _command;
    private readonly SqliteDataReader _reader;
    private bool _finished;

    /// <summary>Takes ownership of an executed command and its open reader, and captures the column metadata.</summary>
    /// <param name="connection">Connection that runs the command; interrupted on cancellation.</param>
    /// <param name="commandTimeoutSeconds">Command timeout in seconds, 0 for none.</param>
    /// <param name="command">Command that produced <paramref name="reader"/>; disposed with this instance.</param>
    /// <param name="reader">Open reader positioned before the first row.</param>
    public SqliteResultReader(SqliteConnection connection, int commandTimeoutSeconds, SqliteCommand command, SqliteDataReader reader)
    {
        _connection = connection;
        _commandTimeoutSeconds = commandTimeoutSeconds;
        _command = command;
        _reader = reader;
        Columns = ReadColumns(reader);
    }

    private static ColumnInfo[] ReadColumns(SqliteDataReader reader)
    {
        var columns = new ColumnInfo[reader.FieldCount];
        for (var i = 0; i < columns.Length; i++)
            columns[i] = new ColumnInfo(reader.GetName(i), reader.GetDataTypeName(i), reader.GetFieldType(i), true, i);
        return columns;
    }

    /// <inheritdoc />
    public IReadOnlyList<ColumnInfo> Columns { get; private set; }

    /// <inheritdoc />
    public long RecordsAffected => _finished ? _reader.RecordsAffected : -1;

    /// <inheritdoc />
    public ValueTask<object?[][]?> ReadBatchAsync(int maxRows, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxRows);
        return SqliteInterrupt.RunAsync(_connection, _commandTimeoutSeconds, () => ReadRowsAsync(maxRows, cancellationToken), cancellationToken);
    }

    private async ValueTask<object?[][]?> ReadRowsAsync(int maxRows, CancellationToken cancellationToken)
    {
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
    public ValueTask<bool> NextResultAsync(CancellationToken cancellationToken) =>
        SqliteInterrupt.RunAsync(_connection, _commandTimeoutSeconds, () => MoveNextAsync(cancellationToken), cancellationToken);

    private async ValueTask<bool> MoveNextAsync(CancellationToken cancellationToken)
    {
        if (!await _reader.NextResultAsync(cancellationToken))
        {
            _finished = true;
            return false;
        }
        Columns = ReadColumns(_reader);
        _finished = false;
        return true;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _reader.DisposeAsync();
        await _command.DisposeAsync();
    }
}
