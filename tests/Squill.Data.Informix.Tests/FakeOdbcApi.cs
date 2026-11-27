namespace Squill.Data.Informix.Tests;

/// <summary>One scripted result set of <see cref="FakeOdbcApi"/>; a null cell is SQL NULL.</summary>
public sealed record FakeResult(string[] Columns, List<string?[]> Rows, int RowCount = -1);

/// <summary>A scripted ODBC driver that follows the <c>SQLGetData</c> chunking rules and records every handle call.</summary>
public sealed class FakeOdbcApi : IOdbcApi
{
    private readonly Dictionary<(nint, ushort), int> _offsets = [];
    private nint _next = 100;
    private int _row = -1;
    private int _result;

    public List<string> Log { get; } = [];

    public List<FakeResult> Results { get; } = [];

    /// <summary>Gets or sets the name of the call that fails with -1: AllocEnv, SetEnvAttr, AllocDbc, Connect, AllocStmt, Exec, Fetch.</summary>
    public string? FailAt { get; set; }

    public List<(string State, int Native, string Message)> Diagnostics { get; } = [];

    /// <summary>Gets or sets the return code of Exec (1 = success with info, 100 = no data).</summary>
    public short ExecReturn { get; set; }

    public bool ReportNoTotal { get; set; }

    private FakeResult Current => Results[_result];

    private short Fail(string step) => FailAt == step ? (short)-1 : (short)0;

    public short AllocHandle(short handleType, nint input, out nint output)
    {
        var step = handleType switch { 1 => "AllocEnv", 2 => "AllocDbc", _ => "AllocStmt" };
        Log.Add("alloc " + step);
        output = FailAt == step ? 0 : _next++;
        return Fail(step);
    }

    public short FreeHandle(short handleType, nint handle)
    {
        Log.Add("free " + (handleType switch { 1 => "env", 2 => "dbc", _ => "stmt" }));
        return 0;
    }

    public short SetEnvAttr(nint env, int attribute, nint value) => Fail("SetEnvAttr");

    public short DriverConnect(nint connection, string connectionString)
    {
        Log.Add("connect");
        return Fail("Connect");
    }

    public short Disconnect(nint connection)
    {
        Log.Add("disconnect");
        return 0;
    }

    public short ExecDirect(nint statement, string sql)
    {
        _result = 0;
        _row = -1;
        return FailAt == "Exec" ? (short)-1 : ExecReturn;
    }

    public short NumResultCols(nint statement, out short count)
    {
        count = (short)Current.Columns.Length;
        return 0;
    }

    public short DescribeCol(nint statement, ushort column, out string name, out short sqlType)
    {
        name = Current.Columns[column - 1];
        sqlType = 12;
        return 0;
    }

    public short Fetch(nint statement)
    {
        _offsets.Clear();
        if (FailAt == "Fetch")
            return -1;
        return ++_row < Current.Rows.Count ? (short)0 : (short)100;
    }

    public short GetData(nint statement, ushort column, Span<char> buffer, out nint indicator)
    {
        var value = Current.Rows[_row][column - 1];
        indicator = 0;
        if (value is null)
        {
            indicator = unchecked((nint)0xFFFFFFFFL); // only 4 bytes written (-1 as int), like the real driver
            return 0;
        }

        _offsets.TryGetValue((statement, column), out var offset);
        if (offset > 0 && offset >= value.Length)
            return 100;
        var left = value.Length - offset;
        var take = Math.Min(left, buffer.Length - 1);
        value.AsSpan(offset, take).CopyTo(buffer);
        buffer[take] = '\0';
        _offsets[(statement, column)] = offset + take;
        var truncated = take < left;
        indicator = truncated && ReportNoTotal ? -4 : left * 2;
        return truncated ? (short)1 : (short)0;
    }

    public short MoreResults(nint statement)
    {
        if (_result + 1 >= Results.Count)
            return 100;
        _result++;
        _row = -1;
        return 0;
    }

    public short RowCount(nint statement, out int rows)
    {
        rows = Current.RowCount;
        return 0;
    }

    public short CloseCursor(nint statement)
    {
        Log.Add("closecursor");
        return 0;
    }

    public bool GetDiagRec(short handleType, nint handle, short record, out string sqlState, out int nativeError, out string message)
    {
        if (record > Diagnostics.Count)
        {
            (sqlState, nativeError, message) = ("", 0, "");
            return false;
        }

        (sqlState, nativeError, message) = Diagnostics[record - 1];
        return true;
    }
}
