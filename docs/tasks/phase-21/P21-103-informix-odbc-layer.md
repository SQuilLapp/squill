# P21-103 - Data.Informix: own ODBC layer (handles, connect, execute, text fetch, diagnostics)

Status: todo
Depends on: P21-10

## Goal
First of four cards that replace P21-11 (split, see there): a new `src/Squill.Data.Informix` project whose internal ODBC layer connects to the Informix developer container through IBM's `db2cli64.dll` and runs a statement, reading every value as text. No provider, no UI yet.

Exists today: nothing under `src/`. The throw-away probe `tools/Squill.AotProbe.InformixOdbc` (raw mode, about 60 lines) proves `SQLAllocHandle`, `SQLSetEnvAttr`, `SQLDriverConnectW`, `SQLExecDirectW`, `SQLNumResultCols`, `SQLFetch`, `SQLGetData` (`SQL_C_WCHAR`), `SQLGetDiagRecW`, `SQLFreeHandle` under Native AOT ([report](../../poc/informix-driver.md), option D). `eng/run-docker-tests.ps1 -Engine informix` exists (P21-10) and stops until `tests/Squill.Data.Informix.Tests` exists. Re-read the probe and check each claim before reusing it.

## Design decisions (final)
- Project `src/Squill.Data.Informix` (minimal csproj) and `tests/Squill.Data.Informix.Tests`; added to `Squill.slnx` and `Squill.Windows.slnx`. No NuGet package.
- `OdbcNative`: internal `static partial class` of `LibraryImport` (source generated, no reflection, signatures valid under `DisableRuntimeMarshalling`) with `SQLAllocHandle`, `SQLFreeHandle`, `SQLSetEnvAttr`, `SQLDriverConnectW`, `SQLDisconnect`, `SQLExecDirectW`, `SQLNumResultCols`, `SQLDescribeColW`, `SQLFetch`, `SQLGetData`, `SQLMoreResults`, `SQLRowCount`, `SQLCloseCursor`, `SQLGetDiagRecW`. Only what this card uses; P21-104 adds `SQLPrepareW`, `SQLBindParameter`, `SQLCancel`, `SQLSetConnectAttrW`, `SQLEndTran`.
- `IOdbcApi`: internal interface in front of `OdbcNative` (same calls, handles as `nint`), so the layer is unit-tested against a fake.
- Library resolution: `NativeLibrary.SetDllImportResolver` on the assembly, explicit folder only: `SQUILL_CLIDRIVER` if set, else `<app folder>/clidriver/bin`; never `PATH`, never the registry (`db2cli install -setup` is not used). A missing folder or file throws an exception naming the variable and the folder, like the Oracle bridge.
- `OdbcConnection` (internal sealed): `OpenAsync(connectionString)` (environment handle with ODBC 3, connection handle, `SQLDriverConnectW`), `ExecuteAsync(sql)` returning a forward-only text reader (`FieldCount`, `ColumnName(i)`, `ColumnSqlType(i)`, `ReadAsync()`, `GetText(i)` as `string?` with a `SQLGetData` loop for values longer than the buffer, `NextResultAsync()`, `RowsAffected`), `Dispose` (disconnect, free handles in order). Every ODBC call runs off the caller thread, results are checked through one `Check(rc, handleType, handle)` that throws `OdbcException` (SQLSTATE, native code, message from `SQLGetDiagRecW`, all records joined; `SQL_SUCCESS_WITH_INFO` is not an error).
- `SQLLEN` is `nint` but the driver may write 4 bytes: initialise every indicator to 0 and compare as `int` (found in the probe).
- `OdbcConnectionString.Build(host, port, database, user, password)`: `DATABASE=<db>;HOSTNAME=<host>;PORT=<port>;PROTOCOL=TCPIP;UID=<user>;PWD=<password>` with `{}` escaping of values containing `;` or `}`; the password never appears in an exception message, a log line or `ToString()`.

## Out of scope
- Prepared statements, parameters, `BYTE` / long data, cancel, transactions (P21-104); `IDatabaseConnection`, `DatabaseDialect.Informix`, composition roots, UI (P21-106); schema reading (P21-105).

## Tasks
- [ ] Unit tests first (fake `IOdbcApi`): success path, several result sets, `SQL_NO_DATA`, a value longer than the buffer (2 chunks), `SQL_NULL_DATA`, an error with two diagnostic records, `SQL_SUCCESS_WITH_INFO`, handle release order on failure of each step, connection string escaping and "password not in any message", the resolver choosing `SQUILL_CLIDRIVER` then the app folder and the error text when absent.
- [ ] Docker fixture and tests (`pwsh eng/run-docker-tests.ps1 -Engine informix`; image `icr.io/informix/informix-developer-database:latest`, `LICENSE=accept`, ports 9088 and 9089, login `informix` / `in4mix`, database created with `dbaccess` as in the P21-10 report; the harness waits for readiness): connect, `select first 3 tabname from systables`, a wrong password gives `SQL30082N`, `SQUILL_CLIDRIVER` pointing to the `clidriver` of the `Net.IBM.Data.Db2` package files in the NuGet cache (the test never downloads: skip with a clear message when absent, a skipped test is never reported as verified).
- [ ] `rtk dotnet build -warnaserror` and the analyzers: 0 warnings. The AOT publish of the layer is checked with the WinUI head in P21-106.
- [ ] Done notes: the `clidriver` files used and how the test finds them.

## Acceptance criteria
- Recipe 2 common acceptance; `-Engine informix` green (the connect and read tests); no `NoWarn`, no pragma.

## Risks
- `SQLGetData` returns the length in bytes for `SQL_C_WCHAR` and may report `SQL_NO_TOTAL`: loop until `SQL_NO_DATA`.
- The test needs the 125 MB `clidriver` on the machine; the fixture documents where it is looked for.

## Suggested commit message
Add the Informix ODBC layer with connect, execute and text fetch
