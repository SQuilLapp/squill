using Microsoft.Data.SqlClient;
using Squill.Core;

namespace Squill.Data.SqlServer;

/// <summary>Streams the rows of one executed SQL Server command in batches.</summary>
internal sealed class SqlServerResultReader : IResultReader
{
    private readonly SqlCommand _command;
    private readonly SqlDataReader _reader;
    private readonly List<string> _messages;
    private readonly Action _detach;
    private bool _finished;

    /// <summary>Takes ownership of an executed command and its open reader, and captures the column metadata.</summary>
    /// <param name="command">Command that produced <paramref name="reader"/>; disposed with this instance.</param>
    /// <param name="reader">Open reader positioned before the first row.</param>
    /// <param name="messages">Filled by the connection with the server messages received while the command runs.</param>
    /// <param name="detach">Unsubscribes the message handler; called on disposal.</param>
    public SqlServerResultReader(SqlCommand command, SqlDataReader reader, List<string> messages, Action detach)
    {
        _command = command;
        _reader = reader;
        _messages = messages;
        _detach = detach;
        Columns = ReadColumns(reader);
    }

    // CLR user-defined types (hierarchyid, geography, geometry) need Microsoft.SqlServer.Types, which is reflection-based and not shipped:
    // they come back as their serialized bytes (shown as hex) instead of throwing.
    private static Type FieldType(SqlDataReader reader, int i)
    {
        try
        {
            return reader.GetFieldType(i);
        }
        catch (Exception e) when (e is IOException or InvalidCastException or NotSupportedException)
        {
            return typeof(byte[]);
        }
    }

    private static object ReadValue(SqlDataReader reader, int i)
    {
        try
        {
            return reader.GetValue(i);
        }
        catch (Exception e) when (e is IOException or InvalidCastException or NotSupportedException)
        {
            var length = (int)reader.GetBytes(i, 0, null, 0, 0);
            var bytes = new byte[length];
            reader.GetBytes(i, 0, bytes, 0, length);
            return bytes;
        }
    }

    private static ColumnInfo[] ReadColumns(SqlDataReader reader)
    {
        var columns = new ColumnInfo[reader.FieldCount];
        for (var i = 0; i < columns.Length; i++)
            columns[i] = new ColumnInfo(reader.GetName(i), reader.GetDataTypeName(i), FieldType(reader, i), true, i);
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
        try
        {
            return await ReadRowsAsync(maxRows, cancellationToken);
        }
        catch (SqlException ex) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(ex.Message, ex, cancellationToken);
        }
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
                row[i] = _reader.IsDBNull(i) ? null : ReadValue(_reader, i);
            rows.Add(row);
        }
        return rows.Count == 0 ? null : [.. rows];
    }

    /// <inheritdoc />
    public async ValueTask<bool> NextResultAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!await _reader.NextResultAsync(cancellationToken))
            {
                _finished = true;
                return false;
            }
        }
        catch (SqlException ex) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(ex.Message, ex, cancellationToken);
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
