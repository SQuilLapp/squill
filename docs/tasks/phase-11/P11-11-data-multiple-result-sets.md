# P11-11 - Data: multiple result sets and server messages

Status: todo
Depends on: P11-04

## Goal
A statement that returns several result sets (a SQL Server `EXEC` of a procedure with two `SELECT`s, a MySQL `CALL`, SQLite never, PostgreSQL only through multi-statement text) is read completely, and informational messages (SQL Server `PRINT`/`RAISERROR` severity <= 10, PostgreSQL `RAISE NOTICE`) are returned; today the four `*ResultReader`s read the first result set only (none calls `NextResult`) and drop messages.

## Design decisions (final)
- `IResultReader` (public contract, XML doc) gains `ValueTask<bool> NextResultAsync(CancellationToken ct)` (moves to the next result set, refreshing `Columns` and `RecordsAffected`; returns false when there is none) and `IReadOnlyList<string> Messages { get; }` (server messages received so far, in order; empty when the engine has none). `Columns` stays a property that reflects the current result set. `FakeDatabaseProvider` and every implementer in the repo are updated.
- Per driver: SqlClient `reader.NextResultAsync` + `SqlConnection.InfoMessage` (collect `Errors[].Message`), Npgsql `NextResultAsync` + `NpgsqlConnection.Notice` (`PostgresNotice.MessageText`), MySqlConnector `NextResultAsync` + `InfoMessage`, SQLite `NextResultAsync` (a statement batch has one result set; implement it correctly anyway).
- `RecordsAffected` of the combined reader sums the sets that report a count (document).
- `SqlRunner` (drains the statement) keeps working: it drains all result sets.

## Out of scope
- Showing several results in the UI (P11-12 ... P11-14).

## Tasks
- [ ] Contract, four drivers, tests: SQLite (a single set, `NextResultAsync` false), Docker-gated SQL Server (a procedure with two selects and a `PRINT`), MySQL (`CALL` of a procedure with two selects), PostgreSQL (`DO $$ begin raise notice 'hi'; end $$` gives the message, and `select 1; select 2` in one command text gives two sets; if Npgsql rejects it with parameters, test without).

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` 0 warnings; default test run green; `rtk pwsh eng/run-docker-tests.ps1 -Engine all` green; no container left.
- Driver AOT publishes (`tools/Squill.AotProbe.*`) still succeed with zero IL2xxx/IL3xxx warnings.

## Suggested commit message
Read every result set and server message from the drivers
