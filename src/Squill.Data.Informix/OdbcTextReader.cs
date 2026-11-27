using System.Text;

namespace Squill.Data.Informix;

/// <summary>A forward-only reader over the result sets of one statement; every value is read as text (null for SQL NULL).</summary>
internal sealed class OdbcTextReader : IDisposable
{
    internal const short NoData = 100;
    private const int NullData = -1, NoTotal = -4, BufferChars = 1024;

    private readonly IOdbcApi _api;
    private readonly nint _statement;
    private string[] _names = [];
    private short[] _types = [];
    private string?[] _row = [];
    private bool _disposed;

    internal OdbcTextReader(IOdbcApi api, nint statement)
    {
        _api = api;
        _statement = statement;
    }

    /// <summary>Gets the number of columns of the current result set, 0 for a statement without one.</summary>
    public int FieldCount => _names.Length;

    /// <summary>Gets the rows affected by the statement or the current result, -1 when the driver does not know.</summary>
    public int RowsAffected { get; private set; } = -1;

    /// <summary>Gets the name of a column.</summary>
    /// <param name="index">The 0-based column index.</param>
    /// <returns>The column name.</returns>
    public string ColumnName(int index) => _names[index];

    /// <summary>Gets the ODBC SQL data type code of a column (for example 12 for <c>VARCHAR</c>, 4 for <c>INTEGER</c>).</summary>
    /// <param name="index">The 0-based column index.</param>
    /// <returns>The code from <c>SQLDescribeCol</c>.</returns>
    public short ColumnSqlType(int index) => _types[index];

    /// <summary>Gets the text of a column of the current row.</summary>
    /// <param name="index">The 0-based column index.</param>
    /// <returns>The text, or <see langword="null"/> for SQL NULL.</returns>
    public string? GetText(int index) => _row[index];

    /// <summary>Moves to the next row and reads all its columns.</summary>
    /// <param name="cancellationToken">Cancels before the call starts.</param>
    /// <returns><see langword="false"/> after the last row.</returns>
    /// <exception cref="OdbcException">The fetch failed.</exception>
    public Task<bool> ReadAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_names.Length == 0)
                return false;
            var rc = _api.Fetch(_statement);
            if (rc == NoData)
                return false;
            OdbcConnection.Check(_api, rc, OdbcConnection.HandleStmt, _statement);
            for (var i = 0; i < _row.Length; i++)
                _row[i] = ReadText((ushort)(i + 1));
            return true;
        }, cancellationToken);

    /// <summary>Moves to the next result set, discarding what is left of the current one.</summary>
    /// <param name="cancellationToken">Cancels before the call starts.</param>
    /// <returns><see langword="false"/> when there is no further result set.</returns>
    /// <exception cref="OdbcException">The driver failed.</exception>
    public Task<bool> NextResultAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var rc = _api.MoreResults(_statement);
            if (rc == NoData)
            {
                _names = [];
                _types = [];
                _row = [];
                return false;
            }

            OdbcConnection.Check(_api, rc, OdbcConnection.HandleStmt, _statement);
            LoadResult();
            return true;
        }, cancellationToken);

    /// <summary>Closes the cursor and frees the statement handle.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        if (_names.Length > 0)
            _api.CloseCursor(_statement);
        _api.FreeHandle(OdbcConnection.HandleStmt, _statement);
    }

    /// <summary>Reads the shape of the current result: column count, names, types and affected rows.</summary>
    internal void LoadResult()
    {
        OdbcConnection.Check(_api, _api.NumResultCols(_statement, out var count), OdbcConnection.HandleStmt, _statement);
        _names = new string[count];
        _types = new short[count];
        _row = new string?[count];
        for (var i = 0; i < count; i++)
            OdbcConnection.Check(_api, _api.DescribeCol(_statement, (ushort)(i + 1), out _names[i], out _types[i]), OdbcConnection.HandleStmt, _statement);
        RowsAffected = _api.RowCount(_statement, out var rows) is 0 or 1 ? rows : -1;
    }

    // SQLGetData: the indicator is the byte length still to come (-1 NULL, -4 unknown); a value longer than the buffer comes in chunks until it fits.
    private string? ReadText(ushort column)
    {
        Span<char> buffer = stackalloc char[BufferChars];
        StringBuilder? text = null;
        while (true)
        {
            var rc = _api.GetData(_statement, column, buffer, out var indicator);
            if (rc == NoData)
                return text?.ToString() ?? "";
            OdbcConnection.Check(_api, rc, OdbcConnection.HandleStmt, _statement);
            var length = (int)indicator; // the driver may write only 4 of the 8 bytes
            if (length == NullData)
                return text?.ToString();
            var capacity = buffer.Length - 1;
            var truncated = length == NoTotal || length / sizeof(char) > capacity;
            var chunk = buffer[..(truncated ? capacity : length / sizeof(char))];
            if (!truncated && text is null)
                return new string(chunk);
            (text ??= new StringBuilder()).Append(chunk);
            if (!truncated)
                return text.ToString();
        }
    }
}
