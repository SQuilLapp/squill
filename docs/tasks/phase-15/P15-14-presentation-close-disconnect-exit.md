# P15-14 - Presentation: open transaction on tab close, disconnect, mode switch and application exit

Status: todo
Depends on: P15-10, P15-12

## Goal
An open transaction is never lost silently: closing the last tab of a session, disconnecting, switching back to auto-commit and quitting the application ask `Valider` / `Annuler` / `Retour`. Recipe: section 2 (decision 10), risk 12; read `WorkspaceViewModel.CloseTabAsync` (~line 246), `DisconnectAsync` (~line 218), `AddTab`, and the fact that **neither head handles window closing today**.

## Design decisions (final)
- `src/Squill.Presentation/Safety/`: `public enum TransactionChoice { Commit, Rollback, Cancel }`, `public sealed record TransactionCloseRequest(string ConnectionName, ConnectionEnvironment Environment, string Database, TransactionState State, int PendingCount, IReadOnlyList<string> Statements, string Title, string Message)` (localized by the view-model; `Statements` = the first five journal texts; on `Failed` the heads hide the `Valider` button), and `IDialogService.AskTransactionAsync(TransactionCloseRequest)` -> `ValueTask<TransactionChoice>` (new member, temporary `ConfirmAsync`-based fallback in both heads returning `Rollback` on yes / `Cancel` on no until P15-16 / P15-18; every test fake gets it).
- A shared helper `internal static class TransactionPrompt { ValueTask<bool> ResolveAsync(ConnectionSession session, IDialogService dialogs, string reasonKey, CancellationToken ct) }` returning `true` when the caller may go on: `State == None` -> true; else asks; `Commit` -> `Transaction.CommitAsync` (error: show `ShowErrorAsync`, return false); `Rollback` -> `Transaction.RollbackAsync` (error: show, return false); `Cancel` -> false. A session with `State == Open` and `PendingCount == 0` and mode `Manual` (nothing written yet) is **not** asked (nothing to lose) but is rolled back silently; opened-by-script transactions with count 0 are asked. The reason keys: `tx.ask.closeTab`, `tx.ask.disconnect`, `tx.ask.switchMode`, `tx.ask.exit`.
- `WorkspaceViewModel.CloseTabAsync`: after its existing questions, when the tab is the **last** tab of `tab.Session` (the session itself, not `Root`) call `ResolveAsync(..., closeTab)`; false cancels the close. Other tabs of the session: no question.
- `DisconnectAsync`: for the root session and every opened derived session (`session.OpenedDatabases.Prepend(session)`), `ResolveAsync(..., disconnect)` in turn; any false aborts the whole disconnect before anything is removed.
- `QueryTabViewModel.SetModeCommand(AutoCommit)` (replaces the `tx.resolveFirst` placeholder of P15-10): `ResolveAsync(..., switchMode)` then `SetModeAsync(AutoCommit)`.
- `WorkspaceViewModel.PrepareExitAsync(CancellationToken ct)` -> `ValueTask<bool>`: resolves every open session and derived session with `tx.ask.exit` (one question per session with a transaction, each naming its connection), returns false on the first `Cancel` or failed commit. It does not close anything itself. A second property `HasOpenTransaction` (any session `State != None`) lets heads skip async work when there is nothing to ask.
- The `Valider` answer of these dialogs is the confirmation for Production sessions (no second dialog, decision 10); the commit still writes the `COMMIT` history entry through a shared method (extract it from `QueryTabViewModel.CommitAsync`, P15-10, into `SessionTransaction`-side helper or a small `internal` service used by both).
- Elapsed text: `WorkspaceViewModel` owns one `TimeProvider.CreateTimer` (30 s) that calls `Transaction.RefreshElapsed()` on every session with `State == Open` through `IUiDispatcher`, started when a transaction opens (it observes `Sessions` and their `Transaction.PropertyChanged`) and disposed with the workspace; `TimeProvider` comes from a new optional constructor parameter defaulting to `TimeProvider.System`.
- Strings: `tx.ask.*` titles / messages (message: `La connexion {0} ({1}) a une transaction ouverte avec {2} instruction(s) non validée(s).`), `tx.ask.commit` = `Valider`, `tx.ask.rollback` = `Annuler les modifications`, `tx.ask.back` = `Retour`, a Failed-state message (`La transaction est en erreur : seule l'annulation est possible.`).

## Out of scope
- The dialog and the closing hook in the heads (P15-16, P15-18), saving a draft of the pending statements.

## Tasks
- [ ] `TransactionChoice`, request, `IDialogService` member + fallbacks + fakes, `TransactionPrompt`, the four call sites, `PrepareExitAsync`, timer, strings.
- [ ] Tests (`tests/Squill.Presentation.Tests/Workspace` and `Safety`): closing the last tab with an open transaction asks and `Retour` keeps the tab, `Annuler` rolls back then closes, `Valider` commits then closes (and a failing commit keeps the tab and shows the error); closing a tab while a second tab of the session remains asks nothing; `Disconnect` with a derived session that has a transaction asks for it by name and aborts everything on `Retour`; mode switch back to `AutoCommit` asks; `PrepareExitAsync` with two sessions asks twice, aborts on the first `Retour`, returns true when nothing is open; `Manual` + `Open` + count 0 is rolled back silently; `Failed` request carries the failed state; elapsed refresh raised by the timer with a fake `TimeProvider`; Production session `Valider` answer does not call `ConfirmProductionAsync`.

## Acceptance criteria
- Common acceptance of the recipe, section 9; localization guards green.

## Suggested commit message
Ask what to do with an open transaction on close, disconnect, mode switch and exit
