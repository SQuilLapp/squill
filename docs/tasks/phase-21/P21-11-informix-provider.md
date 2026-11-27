# P21-11 - Data.Informix: own ODBC layer and provider (connect, execute, stream, schema reading)

Status: split
Depends on: P21-10

## Split (2026-11-27)
Too large for one session (own ODBC layer, typed values, cancel, transactions, schema reader, two heads, packaging, AOT, docs: the PoC estimated 1,200 to 1,800 lines). Replaced by four cards, implemented in this order; the design decisions below stay valid and are distributed among them:

1. [P21-103](P21-103-informix-odbc-layer.md): project, `OdbcNative`, `IOdbcApi`, library resolution, connect, execute, text fetch, diagnostics, Docker fixture.
2. [P21-104](P21-104-informix-odbc-values-cancel-transactions.md): typed values, parameters (`ParameterScanner` mode), cancel, transactions, the database connection.
3. [P21-105](P21-105-informix-schema-reading.md): schema reader and column type decoding.
4. [P21-106](P21-106-informix-provider-registration.md): `DatabaseDialect.Informix`, registration, tiles in both heads, `clidriver` packaging, AOT check, docs.

Owner decision 2026-11-27: shipping `clidriver` is accepted (IBM licence text in the installer, object code only, notices kept); registry writes and global installs are not.

## Goal
A `DatabaseDialect.Informix` profile opens, runs statements, streams results and fills the explorer, through a small own ODBC layer over IBM's `clidriver`.

**Rewritten after P21-10** ([docs/poc/informix-driver.md](../../poc/informix-driver.md)): `Net.IBM.Data.Db2` cannot run in a Native AOT exe (`Open()` throws `NotSupportedException: CodeBase is not supported ...`, IL3002), so the original in-process design is dropped. Verdict: **own ODBC layer (D)**; the bridge (E) is the fallback. If this card finds that the layer cannot read a type or cancel correctly, stop and propose the bridge variant (Oracle pattern) instead of piling workarounds.

Exists today: nothing for Informix. The probe `tools/Squill.AotProbe.InformixOdbc` (raw mode) proves connect, text query and diagnostics with `LibraryImport` into `db2cli64.dll` under AOT; it is throw-away and is not moved into `src/`. Patterns: `Squill.Data.Sqlite` ... `Squill.Data.SqlServer` (provider, connection, result reader, schema reader), `Squill.Data.Oracle` (provider whose driver is not an ADO.NET one).

## Design decisions (final)
- `DatabaseDialect.Informix` (Core), `src/Squill.Data.Informix` (+ `tests/Squill.Data.Informix.Tests`), registered in both heads' composition roots; connection editor tile and engine icon in both heads (small: copy the Oracle tile).
- **ODBC layer**: an internal `OdbcNative` class of `LibraryImport` (source generated, no reflection, no `NoWarn`, `DisableRuntimeMarshalling`-compatible signatures) covering `SQLAllocHandle`/`SQLFreeHandle`, `SQLSetEnvAttr` (ODBC 3), `SQLDriverConnectW`, `SQLDisconnect`, `SQLExecDirectW`, `SQLPrepareW`/`SQLBindParameter`/`SQLExecute`, `SQLNumResultCols`, `SQLDescribeColW`, `SQLFetch`, `SQLGetData` (loop for long values, `SQL_C_WCHAR` text, `SQL_C_BINARY` for `BYTE`), `SQLMoreResults`, `SQLRowCount`, `SQLCancel`, `SQLSetConnectAttrW` (autocommit), `SQLEndTran`, `SQLGetDiagRecW`. `SQLLEN` is `nint` but the driver may write 4 bytes: initialise indicators to 0 and compare as `int` (found in the probe).
- The library is resolved with `NativeLibrary.SetDllImportResolver` from an explicit folder: the `clidriver/bin` folder shipped with the app (`SQUILL_CLIDRIVER` overrides it), never through `PATH`, never through the registry (`db2cli install -setup` is not used). Missing folder: an exception naming the variable and the folder, like the Oracle bridge.
- Packaging: the app ships `clidriver` from the `Net.IBM.Data.Db2` package files (only the redistributable files listed in the package's `REDIST.txt` / `license`, notices kept, IBM licence text in the installer: ask the owner before the first installer change, see the report). Size about 125 MB on disk, 46 MB downloaded.
- Connection string built by the provider: `DATABASE=<db>;HOSTNAME=<host>;PORT=<port>;PROTOCOL=TCPIP;UID=<user>;PWD=<password>` (the password never in logs or on a command line). Port default 9089 = the DRDA listener (`drsoctcp`); a server without one is reported with a hint (message text through `UiText`).
- Profile: host, port (default 9089), database, user; server name is not needed over DRDA. `ServerVersion` over DRDA reports `12.10.0000` whatever the real version: never use it for feature checks.
- Parameters: `?` positional is what the driver binds; the query tab rewrites `:name` to positional in order (a new `ParameterScanner` mode for Informix, tested).
- Values: map by the ODBC SQL type, with `DECIMAL` as `decimal` (text round trip when it does not fit), `DATETIME YEAR TO FRACTION(n)` as `DateTime` read from the ODBC timestamp text/struct, `LVARCHAR`/`VARCHAR`/`CHAR` as string, `BYTE`/`BLOB` as `byte[]`; anything unknown falls back to text. Each column is read once per row.
- Schema: `systables` (`tabtype` T/V, `tabid >= 100`), `syscolumns` (decode `coltype` and `collength` into type text, nullability bit 256), `sysconstraints` + `sysindexes` for PK/FK/indexes, `sysreferences` for FK targets, `sysprocedures`, `systriggers`, `sysviews` for definitions; read with plain SQL through the layer.
- Not built here: designer DDL, grid editing, plans, transactions probe, terminal (`dbaccess`) - each a later card if wanted (list them in the card's Done notes as `P21-97+` proposals).

## Out of scope
- SQL dialect (P21-12), a generic ODBC provider (P21-15 decides), Db2 (P21-13).

## Tasks
- [ ] Unit tests of the type decoding, the connection string, the `:name` to `?` rewrite and the schema mapping (pure), plus the layer against a fake `IOdbcApi` (interface in front of `OdbcNative`) for the result and error paths.
- [ ] `-Engine informix` in `eng/run-docker-tests.ps1` already exists (P21-10); create `tests/Squill.Data.Informix.Tests` and its Docker fixture (image `icr.io/informix/informix-developer-database`, `LICENSE=accept`, port 9089, login `informix` / `in4mix`, database created with `dbaccess`; recipe in the P21-10 report). Tests: connect, run, stream 10,000 rows, cancel, DECIMAL / DATETIME / LVARCHAR / BYTE round trip, schema snapshot of a sample schema.
- [ ] AOT: WinUI head publish with the provider linked in, zero new warnings (`eng/check-aot-warnings.ps1 -Head WinUI`), and the layer run in the published exe against the container.
- [ ] README "Supported databases" row with the measured verdict; `docs/architecture.md` / ADR 0008 row; `docs/poc/informix-driver.md` link.

## Acceptance criteria
- Recipe 2 common acceptance; `-Engine informix` green; WinUI AOT publish baseline only.

## Risks
- `syscolumns.coltype` encodes nullability and type in one number; decoding errors show as wrong types: table-driven tests over every type of the sample schema.
- IBM licence for redistributing `clidriver` (see the report): the installer change needs the owner's go.
- `SQLCancel` and long data (`BYTE`, `TEXT`, `CLOB`) were not measured in P21-10; if they need more than a few dozen lines, switch to the bridge.

## Open points (defaults)
- Owner: accept shipping `clidriver` (125 MB) with IBM's licence text. Default yes.
- Owner: Informix 14.10 or 15.0 as the tested version: default the developer image (15.0.1).

## Suggested commit message
Add the Informix ODBC layer and provider with schema reading
