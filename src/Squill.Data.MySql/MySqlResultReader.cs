using MySqlConnector;
using Squill.Core;

namespace Squill.Data.MySql;

/// <summary>Streams the rows of one executed MySQL command in batches.</summary>
internal sealed class MySqlResultReader : IResultReader
{
    private readonly MySqlCommand _command;
    private readonly MySqlDataReader _reader;
    private readonly List<string> _messages;
    private readonly Action _detach;
    private bool _finished;

    /// <summary>Takes ownership of an executed command and its open reader, and captures the column metadata.</summary>
    /// <param name="command">Command that produced <paramref name="reader"/>; disposed with this instance.</param>
    /// <param name="reader">Open reader positioned before the first row.</param>
    /// <param name="messages">Filled by the connection with the server messages received while the command runs.</param>
    /// <param name="detach">Unsubscribes the message handler; called on disposal.</param>
    public MySqlResultReader(MySqlCommand command, MySqlDataReader reader, List<string> messages, Action detach)
    {
        _command = command;
        _reader = reader;
        _messages = messages;
        _detach = detach;
        Columns = ReadColumns(reader);
    }

    private static ColumnInfo[] ReadColumns(MySqlDataReader reader)
    {
        var columns = new ColumnInfo[reader.FieldCount];
        for (var i = 0; i < columns.Length; i++)
            columns[i] = new ColumnInfo(reader.GetName(i), reader.GetDataTypeName(i), reader.GetFieldType(i), true, i);
        return columns;
    }

    /// <inheritdoc />
    public IReadOnlyList<ColumnInfo> Columns { get; private set; }

    /// <inheritdoc />
    public IReadOnlyList<string> Messages => _messages;

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
    public async ValueTask<bool> NextResultAsync(CancellationToken cancellationToken)
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
        _detach();
        // Disposing an unfinished reader makes the driver drain the rest of the result; stop the statement on the server first.
        if (!_finished)
            _command.Cancel();
        await _reader.DisposeAsync();
        await _command.DisposeAsync();
    }
}
