using System.Runtime.InteropServices;

namespace Squill.Data.Informix;

/// <summary>Source-generated P/Invoke straight into IBM's <c>db2cli64.dll</c> (no driver manager, no registry), located by <see cref="ClidriverLocator"/>.</summary>
internal static partial class OdbcNative
{
    /// <summary>The library name the resolver maps to the bundled <c>clidriver</c> folder.</summary>
    internal const string Library = "db2cli64.dll";

    static OdbcNative() =>
        NativeLibrary.SetDllImportResolver(typeof(OdbcNative).Assembly, (name, _, _) =>
            name == Library ? NativeLibrary.Load(ClidriverLocator.LibraryPath()) : 0);

    [LibraryImport(Library)] internal static partial short SQLAllocHandle(short handleType, nint input, out nint output);
    [LibraryImport(Library)] internal static partial short SQLFreeHandle(short handleType, nint handle);
    [LibraryImport(Library)] internal static partial short SQLSetEnvAttr(nint env, int attribute, nint value, int length);
    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial short SQLDriverConnectW(nint connection, nint window, string input, short inputLength, nint output, short outputMax, nint outputLength, short completion);
    [LibraryImport(Library)] internal static partial short SQLDisconnect(nint connection);
    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf16)] internal static partial short SQLExecDirectW(nint statement, string sql, int length);
    [LibraryImport(Library)] internal static partial short SQLNumResultCols(nint statement, out short count);
    [LibraryImport(Library)]
    internal static unsafe partial short SQLDescribeColW(nint statement, ushort column, char* name, short nameMax, short* nameLength, short* dataType, nuint* size, short* decimals, short* nullable);
    [LibraryImport(Library)] internal static partial short SQLFetch(nint statement);
    [LibraryImport(Library)] internal static unsafe partial short SQLGetData(nint statement, ushort column, short targetType, char* buffer, nint bufferLength, nint* indicator);
    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf16)] internal static partial short SQLPrepareW(nint statement, string sql, int length);
    [LibraryImport(Library)]
    internal static partial short SQLBindParameter(nint statement, ushort number, short inputOutputType, short valueType, short parameterType, nuint columnSize, short decimalDigits, nint value, nint bufferLength, nint indicator);
    [LibraryImport(Library)] internal static partial short SQLExecute(nint statement);
    [LibraryImport(Library)] internal static partial short SQLCancel(nint statement);
    [LibraryImport(Library)] internal static partial short SQLSetConnectAttrW(nint connection, int attribute, nint value, int length);
    [LibraryImport(Library)] internal static partial short SQLEndTran(short handleType, nint handle, short completionType);
    [LibraryImport(Library)] internal static partial short SQLMoreResults(nint statement);
    [LibraryImport(Library)] internal static unsafe partial short SQLRowCount(nint statement, nint* rows);
    [LibraryImport(Library)] internal static partial short SQLCloseCursor(nint statement);
    [LibraryImport(Library)]
    internal static unsafe partial short SQLGetDiagRecW(short handleType, nint handle, short record, char* state, int* native, char* message, short messageMax, short* length);
}
