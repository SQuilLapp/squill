# P21-104 - Data.Informix: typed values, parameters, cancel, transactions and the database connection

Status: done
Depends on: P21-103

## Goal
The ODBC layer reads typed values, binds `?` parameters, cancels a running statement, controls transactions, and is exposed as the SQuilL database connection and result reader of the other providers (same pattern as `Squill.Data.SqlServer` and `Squill.Data.Oracle`). Not yet registered in the app.

Exists today: P21-103 (layer, text reader, `OdbcException`). Check the exact shape of the connection and reader interfaces, `ITransactionProbe`, `DatabaseProfile` and `ParameterScanner` in the code before writing tests (earlier cards had wrong premises).

## Design decisions (final)
- Add to `OdbcNative` / `IOdbcApi`: `SQLPrepareW`, `SQLBindParameter`, `SQLExecute`, `SQLCancel`, `SQLSetConnectAttrW` (autocommit), `SQLEndTran`, and `SQLColAttributeW` if `SQLDescribeColW` is not enough.
- Values by the ODBC SQL type: `INTEGER` / `SMALLINT` / `BIGINT` as `int` / `short` / `long`; `DECIMAL` / `NUMERIC` as `decimal` (text round trip, invariant culture; stays text when it does not fit); `DATETIME YEAR TO FRACTION(n)` as `DateTime` read from the timestamp struct or text (`DATE` as the shared value model has it); `CHAR` / `VARCHAR` / `LVARCHAR` / `NCHAR` as `string`; `BYTE` / `BLOB` as `byte[]` through `SQL_C_BINARY` with a `SQLGetData` loop; `TEXT` / `CLOB` as `string`; anything unknown as text. Each column is read once per row, in order (the driver cannot re-read).
- Parameters: `?` positional bound with `SQLBindParameter` (strings as `SQL_C_WCHAR`, `byte[]` as `SQL_C_BINARY`, `null` as `SQL_NULL_DATA`); `ParameterScanner` gets an Informix mode that rewrites `:name` to `?` in order (`@name` and `$name` are not Informix syntax) and returns the name order. Tested.
- Cancel: `SQLCancel` from another thread while `SQLExecDirectW` / `SQLFetch` runs; the statement fails with a cancel diagnostic mapped to the shared cancelled outcome; the connection stays usable. If this needs more than a few dozen lines or does not work through DRDA, **stop**: report the measurement and propose the bridge variant (Oracle pattern).
- Transactions: autocommit on by default; begin = autocommit off, commit / rollback = `SQLEndTran` then autocommit back; `ITransactionProbe` and the `TransactionSql` texts follow the existing providers (Informix needs a database `with log`: a database without log reports that transactions are unsupported, tested).
- The connection and reader classes implement the shared interfaces; `ServerVersion` over DRDA is `12.10.0000` whatever the real version: show it only, never use it for feature checks (comment in code). Errors become the shared error type with the SQLSTATE.

## Out of scope
- Schema reading (P21-105), `DatabaseDialect.Informix`, composition roots, editor tiles, AOT publish check of the heads (P21-106), grid editing, plans, designer DDL.

## Tasks
- [x] Unit tests first: type mapping over every ODBC type (fake API), `DECIMAL` text round trip edge cases, long `BYTE` in 3 chunks, parameter rewrite (names, repeated names, `?` inside strings and comments untouched), cancel and transaction state machine against the fake.
- [x] Docker tests (`-Engine informix`): run and stream 10,000 rows (cancel half-way, then run another statement on the same connection), `DECIMAL` / `DATETIME YEAR TO FRACTION(5)` / `LVARCHAR` / `BYTE` / `NULL` round trip with `?` parameters, cancel of a long statement (for example a cross join of `systables`), manual commit and rollback in a logged database, an error with its SQLSTATE.
- [x] Done notes: measured behaviour of cancel, long data and transactions; name any workaround.

## Acceptance criteria
- Recipe 2 common acceptance; `-Engine informix` green.

## Risks
- `SQLCancel` and long data were not measured in P21-10: this card measures them and may stop with a bridge proposal.
- `DATETIME` qualifiers other than `YEAR TO FRACTION` (for example `HOUR TO SECOND`) may arrive as `SQL_TYPE_TIME` or text: cover each with a test or fall back to text on purpose.

## Suggested commit message
Add typed values, parameters, cancel and transactions to the Informix connection

## Done notes
- Shipped (all in `src/Squill.Data.Informix`): `IOdbcApi`/`OdbcNative` gained `SQLPrepareW`, `SQLBindParameter`, `SQLExecute`, `SQLCancel`, `SQLSetConnectAttrW`, `SQLEndTran` and a binary `SQLGetData`; `OdbcParameter` (pinned buffers, bound as UTF-16 text or bytes), `OdbcValue` (type map and conversion), `PositionalParameters`, `CancelGate`, `OdbcReader` (the former `OdbcTextReader`, now typed: `GetValue`), `InformixDatabaseConnection` (`IDatabaseConnection`, `ITransactionProbe`), `InformixResultReader`, `InformixTransactionStatement`. Tests: 35 more unit tests on the fake driver, 6 Docker tests (`InformixConnectionTests`); 64/64 green in the Informix suite with `-Engine informix`.
- Premise deviations: (1) `ParameterScanner` lives in `Squill.Presentation`, is keyed by `DatabaseDialect` and rewrites to `@name`, and `DatabaseDialect.Informix` is out of scope: the providers receive `@name` text, so the `@name` to `?` rewrite is in `PositionalParameters` (tested: repeated names, `?`, quotes, `--`, `/* */` and `{ }` untouched); P21-106 adds the dialect, its `:name` tokenizer and `TransactionSql` (see that card). (2) `TransactionSql` has no Informix text yet: the connection recognises `BEGIN`, `COMMIT`, `ROLLBACK` (optionally `WORK`) itself. Savepoints run as plain SQL inside the transaction (not tested). (3) `ReadSchemaAsync` throws `NotSupportedException` until P21-105; `ListDatabasesAsync` reads `sysmaster:sysdatabases`. (4) `ServerVersion` is read with `dbinfo('version','full')` (the real server banner), not the DRDA handshake value.
- Cancel: works through DRDA with `SQLCancel` from the token callback (`CancelGate`, about 30 lines): a 4-way cross join of `systables` stopped 2 s after the token fired (measured 2025 ms from start), the statement fails with an `OdbcException`, mapped to `OperationCanceledException`, and the same connection ran the next statement. Cancel between two fetches is just the token check; disposing the reader closes the cursor. No bridge needed.
- Long data: `BYTE` is reported as ODBC `SQL_BLOB` (-98), read with `SQL_C_BINARY` in 4 KB chunks (10,000 bytes round trip, 3 chunks) and inserted through a `SQL_VARBINARY` parameter without trouble. `LVARCHAR(2000)` with 1500 accented characters round trips.
- Measured type map: `FLOAT` arrives as `DOUBLE`, `DATETIME YEAR TO DAY` as `DATE`, `DATETIME HOUR TO SECOND` as `TIME` (read as `TimeSpan`), `DATETIME YEAR TO FRACTION(5)` as `TIMESTAMP` (`DateTime`, 5 digits kept), `BOOLEAN` as `SMALLINT` (shown as 1/0). `DECIMAL(20,5)` round trips as `decimal`; values with more than 28 digits stay text on purpose.
- Found: the driver writes numbers with the locale decimal separator (`1234567890,12345` on this French machine). Parsed as invariant this became 123456789012345 (the comma read as a thousands separator); `OdbcValue.Convert` now turns `,` into `.` for numeric types before parsing (unit-tested).
- Transactions: autocommit on by default; `BEGIN` = `SQL_ATTR_AUTOCOMMIT` off, `COMMIT`/`ROLLBACK` = `SQLEndTran` then autocommit on; disposing an open transaction rolls back first. Verified in a database `with log` (commit, rollback, visibility from a second connection). In a database without log, `BEGIN` fails with SQLSTATE `HY011` (`CLI0126E Operation invalid at this time`), and the probe stays false. The probe answers from the connection state (no round trip).
- Not run: the Native AOT publish (the layer is checked with the heads in P21-106); a `DECIMAL(p)` without scale, `MONEY` and `INTERVAL` were only looked at, not pinned by tests.
