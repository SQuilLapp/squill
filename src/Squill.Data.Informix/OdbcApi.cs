namespace Squill.Data.Informix;

/// <summary>The real <see cref="IOdbcApi"/>: forwards to <see cref="OdbcNative"/>.</summary>
internal sealed unsafe class OdbcApi : IOdbcApi
{
    private const int SqlCWChar = -8;
    private const short SqlNts = -3, SqlCBinary = -2, SqlParamInput = 1, SqlCommit = 0, SqlRollback = 1;

    /// <summary>Gets the shared instance.</summary>
    public static OdbcApi Instance { get; } = new();

    /// <inheritdoc/>
    public short AllocHandle(short handleType, nint input, out nint output) => OdbcNative.SQLAllocHandle(handleType, input, out output);

    /// <inheritdoc/>
    public short FreeHandle(short handleType, nint handle) => OdbcNative.SQLFreeHandle(handleType, handle);

    /// <inheritdoc/>
    public short SetEnvAttr(nint env, int attribute, nint value) => OdbcNative.SQLSetEnvAttr(env, attribute, value, 0);

    /// <inheritdoc/>
    /// <remarks>The driver calls <c>setlocale(LC_ALL, "")</c> while connecting, which switches the whole process to the user locale; WinUI's native code then formats geometry paths with decimal commas on a French machine and the first tab selection crashes with a <c>XamlParseException</c>. The "C" locale is restored as soon as the call returns.</remarks>
    public short DriverConnect(nint connection, string connectionString)
    {
        try
        {
            return OdbcNative.SQLDriverConnectW(connection, 0, connectionString, SqlNts, 0, 0, 0, 0);
        }
        finally
        {
            OdbcNative.RestoreCLocale();
        }
    }

    /// <inheritdoc/>
    public short Disconnect(nint connection) => OdbcNative.SQLDisconnect(connection);

    /// <inheritdoc/>
    public short ExecDirect(nint statement, string sql) => OdbcNative.SQLExecDirectW(statement, sql, SqlNts);

    /// <inheritdoc/>
    public short NumResultCols(nint statement, out short count) => OdbcNative.SQLNumResultCols(statement, out count);

    /// <inheritdoc/>
    public short DescribeCol(nint statement, ushort column, out string name, out short sqlType)
    {
        var buffer = new char[256];
        short length = 0, type = 0;
        short rc;
        fixed (char* p = buffer)
            rc = OdbcNative.SQLDescribeColW(statement, column, p, (short)buffer.Length, &length, &type, null, null, null);
        if (length >= buffer.Length && rc is 0 or 1)
        {
            buffer = new char[length + 1];
            fixed (char* p = buffer)
                rc = OdbcNative.SQLDescribeColW(statement, column, p, (short)buffer.Length, &length, &type, null, null, null);
        }

        name = new string(buffer, 0, Math.Clamp((int)length, 0, buffer.Length - 1));
        sqlType = type;
        return rc;
    }

    /// <inheritdoc/>
    public short Fetch(nint statement) => OdbcNative.SQLFetch(statement);

    /// <inheritdoc/>
    public short GetData(nint statement, ushort column, Span<char> buffer, out nint indicator)
    {
        nint length = 0; // SQLLEN is 8 bytes but the driver may write 4: start from zero.
        short rc;
        fixed (char* p = buffer)
            rc = OdbcNative.SQLGetData(statement, column, SqlCWChar, p, buffer.Length * sizeof(char), &length);
        indicator = length;
        return rc;
    }

    /// <inheritdoc/>
    public short Prepare(nint statement, string sql) => OdbcNative.SQLPrepareW(statement, sql, SqlNts);

    /// <inheritdoc/>
    public short BindParameter(nint statement, ushort number, OdbcParameter parameter) =>
        OdbcNative.SQLBindParameter(statement, number, SqlParamInput, parameter.CType, parameter.SqlType, (nuint)parameter.ColumnSize, (short)parameter.DecimalDigits,
            parameter.DataPointer, parameter.Data.Length, parameter.IndicatorPointer);

    /// <inheritdoc/>
    public short Execute(nint statement) => OdbcNative.SQLExecute(statement);

    /// <inheritdoc/>
    public short Cancel(nint statement) => OdbcNative.SQLCancel(statement);

    /// <inheritdoc/>
    public short SetConnectAttr(nint connection, int attribute, nint value) => OdbcNative.SQLSetConnectAttrW(connection, attribute, value, 0);

    /// <inheritdoc/>
    public short EndTran(nint connection, bool commit) => OdbcNative.SQLEndTran(OdbcConnection.HandleDbc, connection, commit ? SqlCommit : SqlRollback);

    /// <inheritdoc/>
    public short GetBinary(nint statement, ushort column, Span<byte> buffer, out nint indicator)
    {
        nint length = 0;
        short rc;
        fixed (byte* p = buffer)
            rc = OdbcNative.SQLGetData(statement, column, SqlCBinary, (char*)p, buffer.Length, &length);
        indicator = length;
        return rc;
    }

    /// <inheritdoc/>
    public short MoreResults(nint statement) => OdbcNative.SQLMoreResults(statement);

    /// <inheritdoc/>
    public short RowCount(nint statement, out int rows)
    {
        nint count = 0;
        var rc = OdbcNative.SQLRowCount(statement, &count);
        rows = (int)count;
        return rc;
    }

    /// <inheritdoc/>
    public short CloseCursor(nint statement) => OdbcNative.SQLCloseCursor(statement);

    /// <inheritdoc/>
    public bool GetDiagRec(short handleType, nint handle, short record, out string sqlState, out int nativeError, out string message)
    {
        var state = stackalloc char[6];
        var text = new char[1024];
        var native = 0;
        short length = 0;
        short rc;
        fixed (char* p = text)
            rc = OdbcNative.SQLGetDiagRecW(handleType, handle, record, state, &native, p, (short)text.Length, &length);
        var ok = rc is 0 or 1;
        sqlState = ok ? new string(state, 0, 5) : "";
        nativeError = native;
        message = ok ? new string(text, 0, Math.Clamp((int)length, 0, text.Length - 1)) : "";
        return ok;
    }
}
