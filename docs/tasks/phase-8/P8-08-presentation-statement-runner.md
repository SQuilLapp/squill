# P8-08 - Presentation: shared statement runner (refactor of the designer apply flow)

Status: done
Depends on: P7-21, P4-04

## Goal
Extract the "run one statement, drain it, bracket statements in a transaction" code of the table designer into one internal helper that the grid commit (P8-09) reuses. Behaviour of the designer does not change.

## Design decisions (final)
- Read `src/Squill.Presentation/Designer/TableDesignerViewModel.cs` (the apply flow) and `QueryTabViewModel` (reader draining) first. New file `src/Squill.Presentation/Sessions/SqlRunner.cs`, `internal static class SqlRunner` with one-line `<summary>` and XML doc on its public members:
  - `ValueTask<long> ExecuteAsync(IDatabaseConnection connection, string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken ct)`: executes, drains every batch (`ReadBatchAsync(1000, ct)` until null), disposes the reader and returns `RecordsAffected` (`-1` when unknown).
  - `ValueTask<object?[]?> QueryFirstRowAsync(IDatabaseConnection connection, string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken ct)`: returns the first row of the result (or null), then drains and disposes.
  - `ValueTask BeginAsync(IDatabaseConnection c, DatabaseDialect d, CancellationToken ct)` (`BEGIN TRANSACTION` for SqlServer, `BEGIN` otherwise), `ValueTask CommitAsync(...)` (`COMMIT TRANSACTION` / `COMMIT`), `ValueTask RollbackAsync(...)` (`ROLLBACK TRANSACTION` / `ROLLBACK`, **swallows its own exceptions except `OperationCanceledException`**).
- `TableDesignerViewModel` is changed to call these helpers; the statement text, the transaction rule (including `PRAGMA` outside the bracket), error messages and the order of operations stay exactly as they are. No public API change.
- If `tests/Squill.Presentation.Tests` cannot see `internal` members, check `InternalsVisibleTo` in `Squill.Presentation.csproj` (other P4 tests already use internals); add it only if it is really missing.

## Out of scope
- Any new behaviour, grid editing, changing the designer's tests (they must pass unchanged).

## Tasks
- [x] Add `SqlRunner` and switch the designer to it.
- [x] Tests for `SqlRunner` in `tests/Squill.Presentation.Tests` with the fake `IDatabaseConnection` of the designer tests: statements recorded with exact text, parameters forwarded, reader drained and disposed, `RecordsAffected` returned, `QueryFirstRowAsync` with and without rows, begin/commit/rollback text per dialect, rollback swallowing an error but not a cancellation.
- [x] All existing designer tests still pass unmodified.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Extract shared statement runner from the designer
