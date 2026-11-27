namespace Squill.Data.Informix;

/// <summary>The ODBC entry points the layer uses, with handles as <see cref="nint"/> and managed strings; a fake stands in for the driver in unit tests.</summary>
internal interface IOdbcApi
{
    /// <summary>Allocates a handle (<c>SQLAllocHandle</c>).</summary>
    /// <param name="handleType">1 environment, 2 connection, 3 statement.</param>
    /// <param name="input">The parent handle, 0 for an environment.</param>
    /// <param name="output">The new handle.</param>
    /// <returns>The ODBC return code.</returns>
    short AllocHandle(short handleType, nint input, out nint output);

    /// <summary>Frees a handle (<c>SQLFreeHandle</c>).</summary>
    /// <param name="handleType">The handle type.</param>
    /// <param name="handle">The handle.</param>
    /// <returns>The ODBC return code.</returns>
    short FreeHandle(short handleType, nint handle);

    /// <summary>Sets an environment attribute (<c>SQLSetEnvAttr</c>).</summary>
    /// <param name="env">The environment handle.</param>
    /// <param name="attribute">The attribute id.</param>
    /// <param name="value">The integer value.</param>
    /// <returns>The ODBC return code.</returns>
    short SetEnvAttr(nint env, int attribute, nint value);

    /// <summary>Connects without prompting (<c>SQLDriverConnectW</c>).</summary>
    /// <param name="connection">The connection handle.</param>
    /// <param name="connectionString">The driver connection string.</param>
    /// <returns>The ODBC return code.</returns>
    short DriverConnect(nint connection, string connectionString);

    /// <summary>Disconnects (<c>SQLDisconnect</c>).</summary>
    /// <param name="connection">The connection handle.</param>
    /// <returns>The ODBC return code.</returns>
    short Disconnect(nint connection);

    /// <summary>Executes a statement (<c>SQLExecDirectW</c>).</summary>
    /// <param name="statement">The statement handle.</param>
    /// <param name="sql">The SQL text.</param>
    /// <returns>The ODBC return code; 100 when a searched update touched no row.</returns>
    short ExecDirect(nint statement, string sql);

    /// <summary>Counts the columns of the current result (<c>SQLNumResultCols</c>).</summary>
    /// <param name="statement">The statement handle.</param>
    /// <param name="count">The column count, 0 for a statement without result set.</param>
    /// <returns>The ODBC return code.</returns>
    short NumResultCols(nint statement, out short count);

    /// <summary>Describes a column (<c>SQLDescribeColW</c>).</summary>
    /// <param name="statement">The statement handle.</param>
    /// <param name="column">The 1-based column number.</param>
    /// <param name="name">The column name.</param>
    /// <param name="sqlType">The ODBC SQL data type code.</param>
    /// <returns>The ODBC return code.</returns>
    short DescribeCol(nint statement, ushort column, out string name, out short sqlType);

    /// <summary>Advances to the next row (<c>SQLFetch</c>).</summary>
    /// <param name="statement">The statement handle.</param>
    /// <returns>The ODBC return code; 100 after the last row.</returns>
    short Fetch(nint statement);

    /// <summary>Reads a column of the current row as UTF-16 text (<c>SQLGetData</c> with <c>SQL_C_WCHAR</c>).</summary>
    /// <param name="statement">The statement handle.</param>
    /// <param name="column">The 1-based column number.</param>
    /// <param name="buffer">Receives the text and a terminating zero.</param>
    /// <param name="indicator">The length in bytes of what is left, -1 for NULL, -4 when unknown; the driver may write only its low 4 bytes, so read it as <see cref="int"/>.</param>
    /// <returns>The ODBC return code; 1 with a full buffer means more data follows, 100 means the value was already read.</returns>
    short GetData(nint statement, ushort column, Span<char> buffer, out nint indicator);

    /// <summary>Moves to the next result set (<c>SQLMoreResults</c>).</summary>
    /// <param name="statement">The statement handle.</param>
    /// <returns>The ODBC return code; 100 when there is none.</returns>
    short MoreResults(nint statement);

    /// <summary>Gets the rows affected by the last statement (<c>SQLRowCount</c>).</summary>
    /// <param name="statement">The statement handle.</param>
    /// <param name="rows">The count, -1 when unknown.</param>
    /// <returns>The ODBC return code.</returns>
    short RowCount(nint statement, out int rows);

    /// <summary>Closes the open cursor (<c>SQLCloseCursor</c>).</summary>
    /// <param name="statement">The statement handle.</param>
    /// <returns>The ODBC return code.</returns>
    short CloseCursor(nint statement);

    /// <summary>Reads one diagnostic record (<c>SQLGetDiagRecW</c>).</summary>
    /// <param name="handleType">The handle type.</param>
    /// <param name="handle">The handle.</param>
    /// <param name="record">The 1-based record number.</param>
    /// <param name="sqlState">The five-character SQLSTATE.</param>
    /// <param name="nativeError">The native error code.</param>
    /// <param name="message">The message text.</param>
    /// <returns><see langword="true"/> when the record exists.</returns>
    bool GetDiagRec(short handleType, nint handle, short record, out string sqlState, out int nativeError, out string message);
}
