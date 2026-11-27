namespace Squill.Data.Informix.Tests;

/// <summary>One scripted result set of <see cref="FakeOdbcApi"/>; a null cell is SQL NULL.</summary>
/// <param name="Columns">The column names.</param>
/// <param name="Rows">The text of each cell; a binary column holds <c>null</c> here and its bytes in <paramref name="Blobs"/>.</param>
/// <param name="RowCount">What <c>SQLRowCount</c> reports.</param>
/// <param name="Types">The ODBC type code of each column; 12 when omitted.</param>
/// <param name="Blobs">The bytes of binary cells by (row, column).</param>
public sealed record FakeResult(string[] Columns, List<string?[]> Rows, int RowCount = -1, short[]? Types = null, Dictionary<(int Row, int Column), byte[]>? Blobs = null);

/// <summary>A scripted ODBC driver that follows the <c>SQLGetData</c> chunking rules and records every handle call.</summary>
internal sealed class FakeOdbcApi : IOdbcApi
{
    private readonly Dictionary<(nint, ushort), int> _offsets = [];
    private nint _next = 100;
    private int _row = -1;
    private int _result;

    public List<string> Log { get; } = [];

    public List<string> Statements { get; } = [];

    public List<FakeResult> Results { get; } = [];

    /// <summary>Gets or sets the name of the call that fails with -1: AllocEnv, SetEnvAttr, AllocDbc, Connect, AllocStmt, Exec, Fetch.</summary>
    public string? FailAt { get; set; }

    public List<(string State, int Native, string Message)> Diagnostics { get; } = [];

    /// <summary>Gets or sets the return code of Exec (1 = success with info, 100 = no data).</summary>
    public short ExecReturn { get; set; }

    public bool ReportNoTotal { get; set; }

    /// <summary>Gets the parameters bound by the last statement, in marker order.</summary>
    public List<OdbcParameter> Bound { get; } = [];

    /// <summary>Gets or sets a gate that makes the statement block until <see cref="Cancel"/> is called, then fail like a cancelled one.</summary>
    public ManualResetEventSlim? BlockUntilCancelled { get; set; }

    /// <summary>Gets or sets the return code of <see cref="EndTran"/> and of the autocommit switch.</summary>
    public short TransactionReturn { get; set; }

    public ManualResetEventSlim Started { get; } = new();

    public int CancelCount { get; private set; }

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
        Statements.Add(sql);
        Bound.Clear();
        return Exec();
    }

    public short Prepare(nint statement, string sql)
    {
        Statements.Add("prepare " + sql);
        Bound.Clear();
        return 0;
    }

    public short BindParameter(nint statement, ushort number, OdbcParameter parameter)
    {
        Bound.Add(parameter);
        return 0;
    }

    public short Execute(nint statement) => Exec();

    private short Exec()
    {
        _result = 0;
        _row = -1;
        if (BlockUntilCancelled is { } gate)
        {
            Started.Set();
            gate.Wait(TimeSpan.FromSeconds(10));
            Diagnostics.Add(("HY008", -952, "Operation cancelled."));
            return -1;
        }

        return FailAt == "Exec" ? (short)-1 : ExecReturn;
    }

    public short Cancel(nint statement)
    {
        CancelCount++;
        Log.Add("cancel");
        BlockUntilCancelled?.Set();
        return 0;
    }

    public short SetConnectAttr(nint connection, int attribute, nint value)
    {
        Log.Add($"attr {attribute}={value}");
        return TransactionReturn;
    }

    public short EndTran(nint connection, bool commit)
    {
        Log.Add(commit ? "commit" : "rollback");
        return TransactionReturn;
    }

    public short GetBinary(nint statement, ushort column, Span<byte> buffer, out nint indicator)
    {
        indicator = 0;
        var blob = Current.Blobs![(_row, column - 1)];
        _offsets.TryGetValue((statement, column), out var offset);
        if (offset > 0 && offset >= blob.Length)
            return 100;
        var left = blob.Length - offset;
        var take = Math.Min(left, buffer.Length);
        blob.AsSpan(offset, take).CopyTo(buffer);
        _offsets[(statement, column)] = offset + take;
        indicator = left;
        return take < left ? (short)1 : (short)0;
    }

    public short NumResultCols(nint statement, out short count)
    {
        count = (short)Current.Columns.Length;
        return 0;
    }

    public short DescribeCol(nint statement, ushort column, out string name, out short sqlType)
    {
        name = Current.Columns[column - 1];
        sqlType = Current.Types?[column - 1] ?? 12;
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
