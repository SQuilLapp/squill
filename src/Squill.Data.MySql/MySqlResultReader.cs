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
    private int[] _bitWidths;
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
        _bitWidths = ReadBitWidths(reader);
    }

    // BIT(n) columns read as ulong; the width tells a flag (BIT(1) -> bool) from a bit string (BIT(n) -> big-endian bytes, shown as hex).
    private static int[] ReadBitWidths(MySqlDataReader reader)
    {
        var widths = new int[reader.FieldCount];
        var schema = reader.GetColumnSchema();
        for (var i = 0; i < widths.Length; i++)
            if (reader.GetFieldType(i) == typeof(ulong) && string.Equals(reader.GetDataTypeName(i), "BIT", StringComparison.OrdinalIgnoreCase))
                widths[i] = schema[i].ColumnSize ?? 64;
        return widths;
    }

    private object? ReadValue(int i)
    {
        var value = _reader.GetValue(i);
        if (value is not ulong bits || _bitWidths[i] == 0)
            return value;
        if (_bitWidths[i] == 1)
            return bits != 0;
        var bytes = new byte[(_bitWidths[i] + 7) / 8];
        for (var b = 0; b < bytes.Length; b++)
            bytes[b] = (byte)(bits >> (8 * (bytes.Length - 1 - b)));
        return bytes;
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
                row[i] = _reader.IsDBNull(i) ? null : ReadValue(i);
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
        _bitWidths = ReadBitWidths(_reader);
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
