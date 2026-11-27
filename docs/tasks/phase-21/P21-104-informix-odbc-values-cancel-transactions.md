# P21-104 - Data.Informix: typed values, parameters, cancel, transactions and the database connection

Status: todo
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
- [ ] Unit tests first: type mapping over every ODBC type (fake API), `DECIMAL` text round trip edge cases, long `BYTE` in 3 chunks, parameter rewrite (names, repeated names, `?` inside strings and comments untouched), cancel and transaction state machine against the fake.
- [ ] Docker tests (`-Engine informix`): run and stream 10,000 rows (cancel half-way, then run another statement on the same connection), `DECIMAL` / `DATETIME YEAR TO FRACTION(5)` / `LVARCHAR` / `BYTE` / `NULL` round trip with `?` parameters, cancel of a long statement (for example a cross join of `systables`), manual commit and rollback in a logged database, an error with its SQLSTATE.
- [ ] Done notes: measured behaviour of cancel, long data and transactions; name any workaround.

## Acceptance criteria
- Recipe 2 common acceptance; `-Engine informix` green.

## Risks
- `SQLCancel` and long data were not measured in P21-10: this card measures them and may stop with a bridge proposal.
- `DATETIME` qualifiers other than `YEAR TO FRACTION` (for example `HOUR TO SECOND`) may arrive as `SQL_TYPE_TIME` or text: cover each with a test or fall back to text on purpose.

## Suggested commit message
Add typed values, parameters, cancel and transactions to the Informix connection
