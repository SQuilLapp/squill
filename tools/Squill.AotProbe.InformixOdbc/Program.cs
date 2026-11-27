using System.Data.Odbc;
using System.Runtime.InteropServices;
using System.Text;

// usage: Squill.AotProbe.InformixOdbc sdo "<odbc connection string>" "<sql>"   (System.Data.Odbc over the driver manager)
//        Squill.AotProbe.InformixOdbc raw "<CLI connection string>" "<sql>"     (own P/Invoke straight into db2cli64.dll, no driver manager, no registry)
try
{
    if (args.Length != 3)
    {
        Console.WriteLine("usage: Squill.AotProbe.InformixOdbc sdo|raw <connection string> <sql>");
        return 2;
    }

    if (args[0] == "sdo")
        Sdo(args[1], args[2]);
    else
        Raw.Run(args[1], args[2]);
    Console.WriteLine("OK");
    return 0;
}
catch (Exception ex)
{
    Console.WriteLine(ex);
    return 1;
}

static void Sdo(string connectionString, string sql)
{
    using var connection = new OdbcConnection(connectionString);
    connection.Open();
    Console.WriteLine("server " + connection.ServerVersion + " driver " + connection.Driver);
    using var command = new OdbcCommand(sql, connection);
    using var reader = command.ExecuteReader();
    for (var i = 0; i < reader.FieldCount; i++)
        Console.WriteLine($"  col {reader.GetName(i)} {reader.GetDataTypeName(i)} {reader.GetFieldType(i)}");
    while (reader.Read())
    {
        var cells = new string[reader.FieldCount];
        for (var i = 0; i < cells.Length; i++)
            cells[i] = reader.IsDBNull(i) ? "NULL" : reader.GetValue(i).ToString() ?? "";
        Console.WriteLine("  " + string.Join(" | ", cells));
    }
}

internal static partial class Raw
{
    private const string Lib = "db2cli64.dll";
    private const short SqlHandleEnv = 1, SqlHandleDbc = 2, SqlHandleStmt = 3, SqlNtsL = -3; // SQL_NTS as nint below
    private const int SqlAttrOdbcVersion = 200, SqlOv3 = 3, SqlDriverNoPrompt = 0, SqlCWChar = -8;

    [LibraryImport(Lib)] private static partial short SQLAllocHandle(short type, nint input, out nint output);
    [LibraryImport(Lib)] private static partial short SQLSetEnvAttr(nint env, int attr, nint value, int length);
    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf16)]
    private static partial short SQLDriverConnectW(nint dbc, nint window, string input, short inputLength, nint output, short outputMax, nint outputLength, short completion);
    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf16)]
    private static partial short SQLExecDirectW(nint stmt, string sql, int length);
    [LibraryImport(Lib)] private static partial short SQLNumResultCols(nint stmt, out short count);
    [LibraryImport(Lib)] private static partial short SQLFetch(nint stmt);
    [LibraryImport(Lib)] private static unsafe partial short SQLGetData(nint stmt, ushort column, short targetType, char* buffer, nint bufferLength, nint* indicator);
    [LibraryImport(Lib)] private static partial short SQLFreeHandle(short type, nint handle);
    [LibraryImport(Lib)] private static unsafe partial short SQLGetDiagRecW(short type, nint handle, short record, char* state, int* native, char* message, short messageMax, short* length);

    public static unsafe void Run(string connectionString, string sql)
    {
        Check(SQLAllocHandle(SqlHandleEnv, 0, out var env), SqlHandleEnv, 0, "env");
        Check(SQLSetEnvAttr(env, SqlAttrOdbcVersion, SqlOv3, 0), SqlHandleEnv, env, "version");
        Check(SQLAllocHandle(SqlHandleDbc, env, out var dbc), SqlHandleEnv, env, "dbc");
        Check(SQLDriverConnectW(dbc, 0, connectionString, -3, 0, 0, 0, SqlDriverNoPrompt), SqlHandleDbc, dbc, "connect");
        Check(SQLAllocHandle(SqlHandleStmt, dbc, out var stmt), SqlHandleDbc, dbc, "stmt");
        Check(SQLExecDirectW(stmt, sql, -3), SqlHandleStmt, stmt, "exec");
        Check(SQLNumResultCols(stmt, out var columns), SqlHandleStmt, stmt, "cols");
        var buffer = stackalloc char[512];
        while (SQLFetch(stmt) == 0)
        {
            var cells = new string[columns];
            for (ushort c = 1; c <= columns; c++)
            {
                nint indicator;
                Check(SQLGetData(stmt, c, SqlCWChar, buffer, 1024, &indicator), SqlHandleStmt, stmt, "getdata");
                cells[c - 1] = (int)indicator == -1 ? "NULL" : new string(buffer);
            }

            Console.WriteLine("  " + string.Join(" | ", cells));
        }

        SQLFreeHandle(SqlHandleStmt, stmt);
        SQLFreeHandle(SqlHandleDbc, dbc);
        SQLFreeHandle(SqlHandleEnv, env);
    }

    private static unsafe void Check(short rc, short type, nint handle, string what)
    {
        if (rc is 0 or 1)
            return;
        var state = stackalloc char[6];
        var message = stackalloc char[512];
        int native;
        short length;
        if (handle != 0)
            SQLGetDiagRecW(type, handle, 1, state, &native, message, 512, &length);
        throw new InvalidOperationException($"{what} failed rc={rc}: {(handle != 0 ? new string(state) + " " + new string(message) : "")}");
    }
}
