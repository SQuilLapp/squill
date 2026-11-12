# P15-09 - Presentation: grid commit, designer apply, schema refresh and plans inside an open transaction

Status: todo
Depends on: P15-08

## Goal
Every other flow that uses the session connection stops breaking (or ending) the user's transaction. Today `EditCommitter` and `TableDesignerViewModel` send `BEGIN` / `COMMIT` through `SqlRunner`, which is wrong inside a user transaction on every engine (recipe section 1 and risk 2). Recipe: section 2 (decisions 8, 9), risks 2, 3, 5, 6; read `SqlRunner.cs`, `Editing/EditCommitter.cs` (line ~61), `Designer/TableDesignerViewModel.cs` (line ~245), `ResultEditorViewModel.CommitAsync` (line ~130), `QueryTabViewModel.RefreshAfterSchemaChangeAsync`.

## Design decisions (final)
- A small `internal sealed class StatementScope` created by `SqlRunner.BeginScopeAsync(ConnectionSession session, CancellationToken ct)`: when `session.Transaction.State == None` it behaves exactly as today (`BEGIN` ... `COMMIT`, rollback on failure); when `Open` it runs `SAVEPOINT squill_sp_<n>` (`TransactionSql.Savepoint`, n from a per-session counter), `CompleteAsync` releases it (`ReleaseSavepoint`, nothing on SQL Server) and `FailAsync` does `ROLLBACK TO` the savepoint (never an unnamed rollback, risk 5); when `Failed` it throws `InvalidOperationException(tx.failed)` **before** sending anything. `SqlRunner.BeginAsync/CommitAsync/RollbackAsync` stay for the `Failed`-free paths that still need them and get an XML note saying they must not be used with an open transaction; `EditCommitter` and `TableDesignerViewModel` switch to the scope (they receive the `ConnectionSession`, check their constructors).
- The statements run by the scope are reported to `session.Transaction.ObserveSuccess(...)` (count and journal, tab title = the editor or designer title) so they appear in the pending count; a failure inside `Open` calls `ObserveFailureAsync` (PostgreSQL: the rollback to the savepoint has already recovered the transaction, so the scope calls `ResyncAsync` after it and the state returns to `Open`: test it).
- **MySQL implicit commit**: the designer apply on a MySQL session with `State == Open` (its statements are DDL) asks `IDialogService.ConfirmAsync(tx.implicitCommit.title, tx.implicitCommit.message)` (`Valider et appliquer` / `Annuler`) before running; after it ran `ObserveSuccess` moves the state to `None`. A DDL designer apply on the other engines inside an open transaction runs in the transaction (their DDL is transactional) and counts as pending. The grid commit is DML only (no implicit commit anywhere).
- Operation counter: `EditCommitter` runs, `TableDesignerViewModel` apply, `QueryTabViewModel.RefreshSchemaAsync` / `RefreshAfterSchemaChangeAsync` and the plan commands each hold `session.Transaction.BeginOperation()` for their duration; a commit / rollback pressed meanwhile is disabled.
- Schema refresh while `Failed`: `RefreshAfterSchemaChangeAsync` and the explorer refresh skip the read and set `StatusText` to `tx.failed.refresh` (the server would refuse every statement); while `Open` they run inside the transaction (an uncommitted `CREATE TABLE` is visible to its own session, which is what the user expects).
- Plans: `RunPlanAsync` refuses `Analyser` and `Expliquer` while `State == Failed` with the reason `tx.failed`; inside `Open` both run (the providers already protect the transaction: PostgreSQL warning `25001`, SQL Server savepoint, MySQL no wrapper); nothing in the providers changes.
- Strings: `tx.implicitCommit.*`, `tx.failed.refresh`, reuse of `tx.failed`.

## Out of scope
- The production confirmation for these flows (P15-13), the query tab run path (P15-10), heads.

## Tasks
- [ ] `StatementScope` / `SqlRunner.BeginScopeAsync`, switch `EditCommitter` and `TableDesignerViewModel`, operation counters, schema-refresh and plan guards, strings.
- [ ] Tests in `tests/Squill.Presentation.Tests` (fake connection recording the SQL, per dialect where the text differs): grid commit with no transaction sends `BEGIN` / `COMMIT` as before; inside an open transaction it sends `SAVEPOINT` / `RELEASE` (SQL Server: `SAVE TRANSACTION`, no release) and **never** `BEGIN` / `COMMIT`; a failing edit rolls back to the savepoint only, the user's earlier statements stay pending and the transaction stays `Open` (PostgreSQL included); grid commit and designer refused with `tx.failed` and nothing sent; MySQL designer apply asks and, declined, sends nothing; accepted, the state becomes `None`; other dialects' designer apply counts as pending; statements appear in the journal with the tab title; operation counter disables `CanCommit` during a commit of edits; schema refresh skipped on `Failed`; `Analyser` / `Expliquer` refused on `Failed`, allowed on `Open`. The existing `EditCommitter` and designer tests keep passing unchanged for the no-transaction path.
- [ ] Docker-gated proof, `tests/Shared/GridEditScenario.cs` (extend, do not rewrite): per engine, open a transaction with `SessionTransaction`, grid commit, then `Annuler` removes **both** the earlier statement and the grid edit; `Valider` keeps both. Run with `rtk pwsh eng/run-docker-tests.ps1 -Engine all`.

## Acceptance criteria
- Common acceptance of the recipe, section 9 (Docker run for all engines green, nothing skipped, no container left).

## Suggested commit message
Keep a user transaction intact during grid commits, designer applies and plans
