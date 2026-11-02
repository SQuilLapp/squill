# P8-09 - Presentation: committing a change set

Status: todo
Depends on: P8-03, P8-04, P8-07, P8-08

## Goal
Execute a `ChangeSet` against the connection in one transaction, detect lost rows, and return the generated keys.

## Design decisions (final)
- Namespace `Squill.Presentation.Editing`. `public sealed class EditCommitter(IDatabaseConnection connection, DatabaseDialect dialect)` with `public async Task<CommitResult> CommitAsync(EditTarget target, IReadOnlyList<PendingEdit> edits, CancellationToken ct)`; `public sealed record CommitResult(IReadOnlyDictionary<long, object?> GeneratedKeys, int StatementCount)` (`GeneratedKeys`: grid row of an inserted row -> generated key value, as `AcceptChanges` of P8-06 expects); `public sealed class EditCommitException(string message, long? row, Exception? inner = null) : Exception(message, inner)` with property `Row` (add the standard exception constructors the CA rules ask for).
- Flow: `SqlRunner.BeginAsync` (all dialects, DML is transactional; document that non-transactional MySQL engines such as MyISAM cannot be rolled back); for each edit in the given order: build the statement with `DmlGenerator` (`PlanInsert` for inserts), map `DmlParameter` to `QueryParameter`, execute. **Updates and deletes must report exactly one affected row** (`SqlRunner.ExecuteAsync` result); `0` -> `EditCommitException("Row <n> was not found: it was changed or deleted since it was loaded", row)`, more than one -> `"... matched more than one row"`; `-1` (provider does not report) is accepted. Inserts with a generated key: when `DmlGenerator.ReturnsKeyFromInsert(dialect)` read the first row of the insert's own result with `SqlRunner.QueryFirstRowAsync` and take column 0, otherwise execute the insert with `ExecuteAsync` then `QueryFirstRowAsync(KeyQuery)`; convert the key to the key column's CLR type (`ColumnInfo.ClrType` of the result column) with an explicit `switch` over `int`, `long`, `short`, `byte`, `decimal` and `ulong` (MySQL `LAST_INSERT_ID()` returns `ulong`, SQL Server `OUTPUT` may return `decimal`; no `Convert.ChangeType`); any other type is stored as returned.
- Any exception from the database: `RollbackAsync`, then throw `EditCommitException` with the provider's message and the grid `Row` of the statement that failed (`null` for begin/commit failures); `OperationCanceledException` rolls back and propagates. Success: `CommitAsync`, return the result.
- No UI types, no reflection, no retries.

## Out of scope
- Optimistic concurrency on non-key columns (the `WHERE` uses the key only; documented in P8-18), batching, view-model state (P8-10).

## Tasks
- [ ] Implement the committer.
- [ ] Tests in `tests/Squill.Presentation.Tests` with a fake `IDatabaseConnection` recording statements and parameters and scripting affected counts: order (delete, update, insert) and bracket per dialect, parameters mapped, zero and multiple affected rows roll back and carry the row, provider exception rolls back and carries the row, generated key from the insert result (PostgreSql, Sqlite, SqlServer) and from `LAST_INSERT_ID` (MySql), key type conversion, cancellation rolls back, empty edit list executes nothing.
- [ ] End-to-end test in `tests/Squill.Data.Sqlite.Tests` (reference `Squill.Presentation` if the project does not already; if that creates a cycle or layering violation STOP and propose a card fix, in which case put the test in `tests/Squill.Presentation.Tests` using the real SQLite provider as P7-21 did) on a temp file: create a table, run an insert, an update by key, a delete, verify the rows, verify a failing statement leaves the earlier ones rolled back.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add grid edit committer
