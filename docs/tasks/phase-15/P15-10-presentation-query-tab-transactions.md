# P15-10 - Presentation: transactions in the query tab (run path, Valider, Annuler, mode)

Status: todo
Depends on: P15-08, P15-09

## Goal
`QueryTabViewModel` runs statements through the session transaction and exposes the commands and state the toolbar and status bar bind to. Recipe: section 2 (decisions 2 to 9), risks 1, 3, 4, 10; read `QueryTabViewModel.ExecuteAsync` / `RunStatementAsync` (lines ~300-445) and `RunPlanAsync` for the patterns (`IsRunning`, `_cancel`, history, `StatusText`).

## Design decisions (final)
- `QueryTabViewModel` gets `public SessionTransaction Transaction => _session.Transaction;` and re-raises nothing itself (heads bind to `Transaction` directly), plus commands `CommitCommand` and `RollbackCommand` (disabled unless `Transaction.CanCommit` / `CanRollback` and `!IsRunning`), `SetModeCommand(TransactionMode)` (parameter `TransactionMode`; `Manual` -> `Transaction.SetModeAsync`; `AutoCommit` with an open transaction is resolved by P15-14, for this card it calls `SetModeAsync` only when `State == None` and otherwise sets `StatusText = tx.resolveFirst`), constants `public const string CommitShortcut = "Ctrl+Alt+C"` and `RollbackShortcut = "Ctrl+Alt+Z"` read by the heads (check no existing binding uses them, stop with a proposed fix if one does, risk 10).
- `RunStatementAsync` per statement, after parameter rewriting: `var traits = StatementClassifier.Classify(original, dialect);` then
  1. `Transaction.State == Failed` and `traits.Control` is not `Rollback` / `RollbackToSavepoint`: result message `tx.failed` (a message result like a positional-parameter error), history entry `succeeded: false`, stop the script (same as an error).
  2. `traits.CannotRunInTransaction` while `State != None`: message `tx.cannotRun` naming the statement kind, nothing sent, stop.
  3. `traits.ImplicitCommit` while `State != None`: `ConfirmAsync(tx.implicitCommit.title, tx.implicitCommit.message)`; declined: stop with `query.cancelled`; accepted: run (the state becomes `None` through `ObserveSuccess`).
  4. `await Transaction.EnsureOpenAsync(traits, ct)` (lazy begin in `Manual`); a failure to begin is reported like a statement error.
  5. Run exactly as today; on success `Transaction.ObserveSuccess(original, traits, rows ?? 0, Title)`; on a statement error `await Transaction.ObserveFailureAsync(ex, traits, ct)`; when `traits.MayChangeTransaction` (and not already an error) `await Transaction.ResyncAsync(ct)` after the statement.
  Existing behaviors stay: history entry per statement, schema refresh after DDL, result tabs, cancel. A cancelled statement inside `Open` leaves the state unchanged and calls `ResyncAsync` (PostgreSQL cancel does not abort a transaction by itself but the statement may have been rolled back to nothing; the probe settles it).
- The whole `ExecuteAsync` run holds `Transaction.BeginOperation()`; `ExecuteAsync` returns with `StatusText = tx.busy` when `Transaction.IsBusy` was already true from another source (a commit in progress in another tab).
- `CommitAsync` command: `await Transaction.CommitAsync(ct)`, history entry `COMMIT` (`HistoryEntry` with `profile.Id`, text `COMMIT`, `succeeded` true / false, rows null, elapsed), `StatusText = tx.committed` / the error text; on error show it as a message result (`ResultViewModel.ForMessage`) and keep the state. `RollbackAsync` the same with `ROLLBACK` and `tx.rolledBack`. Both set `IsRunning` like a run (the cancel button cancels the wait, not a half commit: cancellation is checked before sending only). The Production confirmation of `Valider` is added by P15-12, leave a single `ConfirmCommitAsync()` seam method returning `true`.
- Statements typed by the user (`BEGIN`, `COMMIT`, `ROLLBACK`, savepoints) go through the same path; the history shows them as typed.
- Strings: `tx.cannotRun`, `tx.committed`, `tx.rolledBack`, `tx.resolveFirst`, button labels `Valider`, `Annuler`, `Auto-commit`, `Manuel`, the pill text (`1 instruction non validée` / `N instructions non validées` through `FormatPlural`), tooltips.

## Out of scope
- Production confirmation (P15-12, P15-13), the question on close / mode switch / exit (P15-14), drawing.

## Tasks
- [ ] Commands, run-path integration, history entries, strings fr/en.
- [ ] `tests/Squill.Presentation.Tests/Query/QueryTabTransactionTests.cs` with the fake connection: `Manual` mode: `SELECT` sends no begin, `INSERT` sends begin then the statement, a second `INSERT` no second begin, pending count 2; `AutoCommit` mode: nothing changes versus today (no begin, count 0); script of three statements stops at the first error and the count excludes it; PostgreSQL error -> `Failed`, next statement refused with `tx.failed` and not sent, `ROLLBACK` typed clears it; `CannotRunInTransaction` refused; MySQL `CREATE TABLE` inside `Open` asks, declined sends nothing, accepted sends and state `None`; typed `BEGIN` in `AutoCommit` -> `Open` with `Valider` enabled; `CommitCommand` / `RollbackCommand` send the right text per dialect, write history entries, reset the count; commit error keeps the state and shows the message; commands disabled while running; a second tab of the same session sees the same state; cancellation inside `Open` calls the probe; shortcuts constants.

## Acceptance criteria
- Common acceptance of the recipe, section 9; localization guards green; all existing `QueryTabViewModelTests` pass unchanged.

## Suggested commit message
Run statements through the session transaction and add Valider / Annuler commands
