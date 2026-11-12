# P15-12 - Presentation: production confirmation contract, guard, and its use by Run and Valider

Status: done
Depends on: P15-10, P15-11

## Goal
On a Production connection, `Run`, `Run all` and `Valider` ask the M15 confirmation before anything modifying is sent; the contract the heads implement is defined here. Recipe: section 2 (decisions 11, 12), risks 11, 12; mockup `M15-Confirmation-clair-Windows.png` / `-sombre-`. Read `IDialogService`, `QueryTabViewModel.ExecuteAsync` and the `plan.analyze.confirm.*` code of `RunPlanAsync`.

## Design decisions (final)
- `src/Squill.Presentation/Safety/`:
  - `public enum ConfirmationKind { Run, Commit, GridEdit, SchemaChange }`.
  - `public sealed record ConfirmedStatement(string Text, string? Target, StatementKind Kind)`.
  - `public sealed record ProductionConfirmationRequest(ConfirmationKind Kind, string ConnectionName, string Database, IReadOnlyList<ConfirmedStatement> Statements, long? EstimatedRows, string? RequiredText, bool ShowTransactionOption, bool TransactionOptionLocked, string Title, string Message, string ConfirmLabel)`: everything already localized and formatted by the view-model, heads draw it and add nothing (`Message` may contain the figure; the head styles it only if it receives the figure as a separate `EstimatedRows` for the danger color).
  - `public sealed record ProductionConfirmationResult(bool Confirmed, bool UseTransaction)`.
  - `IDialogService.ConfirmProductionAsync(ProductionConfirmationRequest request)` -> `ValueTask<ProductionConfirmationResult>` (new member; both heads get a **temporary** implementation in this card that falls back to `ConfirmAsync(Title, Message)` and returns `UseTransaction = false`, until P15-16 / P15-18 replace it; every test fake gets the member).
- `public static class ProductionGuard` (`internal` helpers public only where the heads need them): `Evaluate(ConnectionProfile profile, IReadOnlyList<string> statements)` -> `ProductionRequirement(bool Required, bool RequiresTyping, IReadOnlyList<StatementTraits> Traits)`: `Required` only when `profile.Environment == Production` and at least one statement is not read-only, **or** is `Commit` control (typed `COMMIT`), **or** is `Call` / `Other` (decision 11); `RequiresTyping` when any traits `Destructive`. `RequiredText(profile)` = `Database`, SQLite file name, else the profile name. `Matches(typed, expected)` = trimmed, ordinal ignore case.
- Building the request (`ProductionRequestBuilder`, internal, pure, tested): title / message / confirm label per the table of recipe decision 12 and these rules: one `DELETE` without `WHERE` -> title `prod.title.deleteAll` with the table, message `prod.message.deleteAll` (+ the figure sentence when `EstimatedRows` is known, + the manual-transaction sentence when the session mode is `Manual`), confirm label `prod.confirm.delete` with the figure (`Supprimer 1 248 310 lignes`) or `prod.confirm.run`; `UPDATE` without `WHERE` -> `modifyAll` / `Modifier N lignes`; any other single statement -> `prod.title.runOne` and the verb by kind when a figure exists (`DELETE` / `UPDATE` with `WHERE`: `Supprimer` / `Modifier N lignes`), else `Exécuter`; several statements -> `prod.title.runMany` (`{0}` count), at most five listed, no figure; `Commit` -> `prod.title.commit`, message with the pending count and environment, the journal as the statement list, label `Valider`; `ShowTransactionOption` = every statement is transactional on the engine (not `ImplicitCommit`, not `CannotRunInTransaction`) and the kind is `Run`; `TransactionOptionLocked` = the session mode is `Manual` (checked, disabled); the option never shows for `Commit`. Number format `UiText.Culture`.
- `QueryTabViewModel.ExecuteAsync`: after `PromptValuesAsync` and before the first statement, `ProductionGuard.Evaluate`; when required: estimate (`ImpactEstimator`, only when there is exactly one statement and the statement is `Update` / `Delete` / `INSERT ... SELECT`), build the request, `ConfirmProductionAsync`; declined: `StatusText = query.cancelled`, nothing sent, no history entry; confirmed with `UseTransaction` and mode `AutoCommit`: `Transaction.SetModeAsync(Manual)` before running (the statements then run lazily inside a new transaction, the user sees `Valider` / `Annuler`); confirmed without it: run auto-committed. One dialog per run, not per statement. The `ConfirmCommitAsync()` seam of P15-10 now asks (kind `Commit`, no typing, no option) when the session is Production and returns its answer; a declined commit leaves everything open.
- The `Analyser` confirmation stays the plain one of P13-14 (a rolled-back read-only statement), unchanged and still tested.
- Strings: `prod.*` (header `prod.header` = `PRODUCTION · {0} / {1}`, titles, messages, labels, `prod.typePrompt` = `Saisissez {0} pour confirmer`, `prod.option`, `prod.hint` = `Entrée = Annuler`, `prod.more` = `+ {0} autres`, `prod.manualActive`), both catalogs, French from M15.

## Out of scope
- Grid commit and designer (P15-13), the real dialogs (P15-16, P15-18), a setting to turn it off.

## Tasks
- [x] Types, guard, request builder, `IDialogService` member + temporary head fallbacks + every test fake, run-path and commit integration, strings.
- [x] Tests `tests/Squill.Presentation.Tests/Safety/`: guard truth table (environment x kind x destructive x read-only x `Call` x typed `COMMIT`), `RequiredText` for the three cases, `Matches`; builder output for each title / label rule in both languages (exact strings), figure formatting (`1 248 310` fr, `1,248,310` en), six statements -> five listed + `+ 1 autres`; query-tab flow with a fake dialog: Production `SELECT` no dialog, Staging `DELETE` no dialog, Production `DELETE` without `WHERE` asks with `RequiredText` and the estimate, declined sends nothing and writes no history, confirmed + option switches to `Manual` and begins a transaction, confirmed without option sends auto-committed, `Manual` mode locks the option, script of two statements asks once, `Valider` on Production asks with the journal and a decline keeps the transaction, estimator failure still asks (no figure), `ImplicitCommit` statement hides the option.

## Acceptance criteria
- Common acceptance of the recipe, section 9; localization guards green; existing `Analyser` confirmation tests unchanged.

## Suggested commit message
Ask the production confirmation before modifying statements and before Valider
