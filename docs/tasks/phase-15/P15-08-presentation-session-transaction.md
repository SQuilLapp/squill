# P15-08 - Presentation: the session transaction (mode, state, pending count, journal)

Status: done
Depends on: P15-01, P15-02, P15-03

## Goal
`ConnectionSession.Transaction` holds the whole transaction logic of a session as a testable state machine with no UI: mode, state, lazy begin, commit, rollback, pending count, journal, resynchronisation with the server, rollback on dispose. Nothing calls it yet (P15-09, P15-10 do). Recipe: section 2 (decisions 1 to 7), section 6; look at `ConnectionSession`, `SqlRunner`, and `tests/Squill.Presentation.Tests/Sessions/SqlRunnerTests.cs` for the fake connection.

## Design decisions (final)
- `public sealed class SessionTransaction : ObservableObject` in `src/Squill.Presentation/Sessions/`, created by `ConnectionSession` (new `public SessionTransaction Transaction { get; }`; the internal constructor gets an optional `TimeProvider? time` for tests, default `TimeProvider.System`). No dispatcher inside: like `QueryTabViewModel` it raises changes from the continuation of the awaiting command (UI thread).
- Properties (all observable, XML-documented): `IsSupported` (`Connection is ITransactionProbe`), `Mode`, `State`, `PendingCount`, `OpenedAt` (null when `State` is `None`), `Journal` (`IReadOnlyList<PendingStatement>`, `PendingStatement(string Text, string Tab, DateTimeOffset At, long RowsAffected)`), `IsBusy` (an operation of another kind is running, or a commit / rollback is), `CanCommit` (`State == Open && !IsBusy`), `CanRollback` (`State != None && !IsBusy`), `StatusText` (localized for the status bar: empty for `AutoCommit` + `None`; `tx.status.manual` for `Manual` + `None`; `tx.status.open` = `Transaction ouverte depuis {duration} · {n instruction(s)}` for `Open`, the duration as `moins d'une minute` / `N min` / `N h NN min`; `tx.status.failed` for `Failed`), `ElapsedText`, and `void RefreshElapsed()` (raises the change for the duration text; the workspace calls it every 30 s, P15-14).
- Methods (every one takes a `CancellationToken`, none runs while `IsBusy` except where said; they throw `InvalidOperationException` with a `tx.*` text when the precondition fails):
  - `ValueTask SetModeAsync(TransactionMode mode, CancellationToken ct)`: `Manual` always allowed when `IsSupported`; `AutoCommit` while `State != None` is **refused** (the caller resolves the open transaction first, P15-14).
  - `ValueTask EnsureOpenAsync(StatementTraits traits, CancellationToken ct)`: only when `Mode == Manual`, `State == None`, `!traits.ReadOnly`, `traits.Control == None`: runs `TransactionSql.Begin` through `SqlRunner.ExecuteAsync` (no begin for Oracle) and sets `State = Open`, `OpenedAt = now`, count 0, journal cleared.
  - `void ObserveSuccess(string statement, StatementTraits traits, long rowsAffected, string tab)`: applies, in this order, (1) `ImplicitCommit` while `Open` -> `None`, count 0; (2) `Control`: `Begin` while `None` -> `Open` (opened by script), `Commit` / `Rollback` -> `None`, `RollbackToSavepoint` while `Failed` -> `Open`, savepoint / release no change; (3) a not-read-only statement with `Control == None` while `Open` -> `PendingCount++` and a journal entry (500 characters max, 200 entries max, the count keeps counting).
  - `ValueTask ObserveFailureAsync(Exception error, StatementTraits traits, CancellationToken ct)`: when `State == Open`: PostgreSQL -> `Failed`; other dialects -> `ResyncAsync`. Never counts the statement.
  - `ValueTask ResyncAsync(CancellationToken ct)`: calls `ITransactionProbe.ProbeAsync`; `InTransaction == false` while `Open` / `Failed` -> `None` (the server ended it: raises `ExternallyEnded` with the reason text `tx.endedByServer`); `InTransaction == true` while `None` -> `Open` (opened by a statement the app did not model); `Failed == true` -> `Failed`; `null` changes nothing.
  - `ValueTask CommitAsync(CancellationToken ct)`: requires `Open` (on `Failed` throws with `tx.failed`), `ResyncAsync` first, runs `TransactionSql.Commit`, then `None`, count 0, journal cleared; on an exception keeps the state, runs `ResyncAsync`, rethrows.
  - `ValueTask RollbackAsync(CancellationToken ct)`: requires `State != None`, runs `TransactionSql.Rollback`, then `None`; on an exception rethrows after `ResyncAsync`.
  - `IDisposable BeginOperation()`: increments an operation counter that makes `IsBusy` true (and the buttons disabled); disposing decrements. `CommitAsync` / `RollbackAsync` themselves throw when the counter is above zero and set `IsBusy` while they run.
  - `ValueTask DisposeAsync()`: when `State != None`, a best-effort `RollbackAsync` that swallows every exception (the connection is about to close). `ConnectionSession.DisposeAsync` awaits it **before** disposing derived sessions and the connection.
- All statement execution uses `SqlRunner.ExecuteAsync` (it drains the reader). The class never touches history, dialogs or the editor.
- Strings (both catalogs): `tx.status.*`, `tx.failed`, `tx.endedByServer`, `tx.unsupported`, `tx.busy`, the duration texts and the plural of `instruction(s)`.

## Out of scope
- Calling it from the query tab, the grid commit, the designer (P15-09, P15-10), dialogs and closing (P15-14), heads.

## Tasks
- [x] `SessionTransaction`, `PendingStatement`, `ConnectionSession.Transaction`, dispose order, strings fr/en.
- [x] `tests/Squill.Presentation.Tests/Sessions/SessionTransactionTests.cs` with a recording fake connection (statements sent, scripted errors, a scripted probe): every row of the transition table of recipe decision 3; lazy begin only for a not-read-only statement in `Manual` and never for `SELECT`; the count and the journal limits (500 characters, 200 entries, count past 200); `Commit` / `Rollback` text per dialect (`START TRANSACTION` on MySQL, `BEGIN TRANSACTION` on SQL Server); PostgreSQL error -> `Failed`, `Rollback` clears it, `Commit` refused; non-PostgreSQL error -> probe called; probe says "ended by server" -> `None` and the event; refusal of `AutoCommit` while `Open`; `IsBusy` while an operation is held (commit throws); typed `BEGIN` / `COMMIT` / `ROLLBACK` / `START TRANSACTION` transitions; MySQL implicit-commit statement while `Open` -> `None`; `StatusText` for the four situations in both languages; dispose sends `ROLLBACK` once and swallows its failure; Oracle-like connection (no probe) -> `IsSupported` false and `SetModeAsync(Manual)` refused.

## Acceptance criteria
- Common acceptance of the recipe, section 9; localization guards green.

## Suggested commit message
Add the session transaction state machine
