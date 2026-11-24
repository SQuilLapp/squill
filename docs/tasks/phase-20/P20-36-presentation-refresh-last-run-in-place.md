# P20-36 - Presentation: refresh the last run of a query tab in place (same results, view kept)

Status: todo
Depends on: P20-33

## Goal
Owner request 2026-11-24: a timed re-execution of the query while looking at a table's data, like DataGrip's auto-refresh clock (`Disabled`, `5 s`, `10 s`, `30 s`, `1 m`, `5 m`, `Custom...`). In SQuilL a table's data is a query tab: `WorkspaceViewModel.OpenTableOnAsync` opens a tab with `SqlSnippets.SelectTop(...)` and runs it, and the only refresh is `Run` again, which clears the results (empty result area, sort, filters, search, selection, scroll and column widths lost), prompts the parameters again and writes a history entry. A refresh every 5 s through that path is unusable.

This card adds the **refresh path** the timer of P20-37 calls: re-run the statements of the last run, without any prompt or history entry, build the new results off screen and swap them in one step, keeping the selected result tab, filters, search, sort and selection. The timer and the interval are P20-37; the buttons are P20-38 (WinUI) and P20-39 (Avalonia).

Files today: `src/Squill.Presentation/Query/QueryTabViewModel.cs` (`ExecuteAsync`, `RunStatementAsync`, `AddResult`, `NextTitle`), `src/Squill.Presentation/Query/QueryTabViewModel.Parameters.cs` (`_lastValues`), `src/Squill.Presentation/Query/ResultViewModel.cs`, `src/Squill.Presentation/Results/ResultViewState.cs`, `src/Squill.Presentation/Results/FilteredGridDataSource.cs` (`WhenIdle`), `src/Squill.Presentation/Results/GridSelection.cs`, `src/Squill.Sql/Transactions/StatementTraits.cs` (`ReadOnly`), `src/Squill.Presentation/Safety/ProductionGuard.cs`.

## Design decisions (final)
- **What a refresh runs**: the statements of the **last run**, not the editor text (the user may edit the script while the table refreshes; DataGrip also refreshes the data view's own query). New private `LastRun` record `(IReadOnlyList<string> Statements, Dictionary<string, ParameterValue> Values)` kept in a field, set at the end of `ExecuteAsync` only when **every** statement completed and the run is **refreshable**:
  - every statement is `StatementClassifier.Classify(sql, dialect).ReadOnly` (a refresh never writes, never opens a manual transaction: `SessionTransaction.EnsureOpenAsync` returns for read-only statements),
  - `ProductionGuard.Evaluate(profile, statements).Required` is false,
  - no statement has positional parameters.
  Any other run (failed, cancelled, DML, DDL, declined prompt) sets it to `null`. Named parameters are refreshed with the values typed for that run (no prompt).
- New members on `QueryTabViewModel` (XML doc on each):
  - `public bool CanRefresh` (observable, `true` while `LastRun` is set),
  - `[ObservableProperty] public partial bool IsRefreshing { get; private set; }` (`true` from the start of a refresh to the end of the swap; the heads use it to keep the completion popup open and not move the focus),
  - `public event Action? ResultsReplacing;` raised just before the swap, `public event Action? ResultsReplaced;` raised just after it (the heads save and restore the grid scroll and column widths around them, P20-38 / P20-39),
  - `internal Task<RefreshOutcome> RefreshAsync(CancellationToken ct)` with `internal enum RefreshOutcome { Refreshed, Failed, Cancelled, Skipped }` (a one-line `<summary>` on the enum and each value).
- **`RefreshAsync` rules** (it shares `ExecuteAsync` / `RunStatementAsync` through a private `RunMode { Run, Refresh }` parameter; no copy of the statement loop):
  - `Skipped` (nothing runs, nothing changes) when `LastRun` is null, `IsRunning`, `Transaction.IsBusy`, `Transaction.State == Failed`, `HasPendingChanges`, `IsParametersPanelOpen`, any result's `EditController.IsEditing` or `Preview.IsDirty`. The caller decides what to say (P20-37).
  - No pending-changes confirmation, no parameter prompt, no production confirmation (already excluded), `_baselineText` untouched (`IsModified` unchanged), **no history entry** (`RunStatementAsync` skips `_history.AppendAsync` in refresh mode: a 5 s refresh would write 720 entries an hour). `Transaction.ObserveSuccess` and the schema-change refresh stay as for a run.
  - `IsRunning` is `true` meanwhile as for a run (Run and Explain disabled, `Stop` cancels it, the 2 px progress bar shows activity).
  - **Staged swap**: in refresh mode `AddResult` adds to a private staged list instead of `Results` and `NextTitle` counts that list; the old results stay on screen while the statements run and the grids stream (`StreamingGridDataSource.StartAsync` drains up to the row limit before returning). At the end, synchronously on the UI thread: `ResultsReplacing`, keep the view of the old results (below), `Release` the old results, `Results.Clear()`, add the staged ones, apply the kept view, `ResultsReplaced`, `IsRefreshing = false`. A statement error is swapped in like a run (the error result is shown once) and returns `Failed`. A cancellation (`Stop`, or the token) **keeps the old results**, releases the staged ones and returns `Cancelled`.
- **Kept view** (only when the new results have the same count and, per grid result, the same column names in the same order, ordinal compare; otherwise the new results show as after a run):
  - the selected result tab (by index),
  - per grid result: filters, search text and sort, through new `internal void CopyFrom(ResultViewState other)` on `ResultViewState` (copies the `GridFilter`s, `SearchText`, `Sort`, then one `Apply`), awaited through new `internal Task WhenIdle()` on `ResultViewState` (`_view?.WhenIdle() ?? Task.CompletedTask`) before the selection is applied, since the filtered view computes off the UI thread,
  - per grid result: the selection, through new `internal void CopyFrom(GridSelection other, long rowCount, int columnCount)` on `GridSelection` (anchor, active cell and `WholeColumns`, clamped to the new grid; cleared when the anchor row is past the new row count). The selection is by row **index**, not by key (a row inserted above moves the selection by one; keyed tracking is out of scope).
  - Order matters: view state first (its change clears the selection, `ResultViewModel.OnViewChanged`), selection last.
- Scroll offsets and column widths are head state (`DataGridCanvas.ScrollX` / `ScrollY`, the measured widths); the heads keep them around `ResultsReplacing` / `ResultsReplaced` (P20-38, P20-39).
- No new user-visible text in this card (no localization key).

## Out of scope
- The timer, the interval, `Custom...`, pausing rules visible to the user, the tab-visible rule and the keys (P20-37); the head buttons and the scroll / width restore (P20-38, P20-39). A manual `Refresh` button (pressing `Run` stays the manual refresh). Keeping the selection on the same database row. Refreshing a DML or a failed run.

## Tasks
- [ ] Tests first, `tests/Squill.Presentation.Tests/Query/QueryTabRefreshTests.cs` (fakes of `QueryTabViewModelTests.cs` / `ViewStateFixture.cs`, `ImmediateDispatcher`, no sleeping: await the returned task):
  - `CanRefresh_AfterSelectRun_True`; `CanRefresh_AfterInsertRun_False`; `CanRefresh_AfterFailedRun_False`; `CanRefresh_ProductionGuardRequired_False` (pick a read-only statement the guard flags if one exists, else drop this test and say why in the Done notes).
  - `Refresh_RerunsLastStatements_NotEditorText` (edit the editor text after the run; the fake connection receives the old SQL); `Refresh_NamedParameters_NoPrompt` (`IsParametersPanelOpen` never true, same values sent).
  - `Refresh_WritesNoHistory`; `Refresh_KeepsIsModified`.
  - `Refresh_OldResultsStayUntilSwap` (a fake reader held on a `TaskCompletionSource`: `Results` still holds the old instances while the refresh runs; after completion they are new instances); `Refresh_RaisesReplacingThenReplaced_Once`.
  - `Refresh_KeepsSelectedResultFiltersSearchSort`; `Refresh_KeepsSelectionClamped` (old selection row 8 of 10, new result 5 rows: clamped or cleared as specified); `Refresh_ColumnsChanged_DoesNotKeepView`.
  - `Refresh_Cancelled_KeepsOldResults`; `Refresh_StatementError_SwapsAndReturnsFailed`.
  - `Refresh_Skipped_WhenPendingChanges` / `_WhenEditing` / `_WhenRunning` / `_WhenTransactionFailed` (each returns `Skipped`, `Results` unchanged, no SQL sent).
  - `tests/Squill.Presentation.Tests/Results/GridSelectionTests.cs` (existing or new): `CopyFrom_ClampsToNewSize`, `CopyFrom_AnchorPastEnd_Clears`, `CopyFrom_WholeColumns_Kept`.
  - See them fail, then implement.
- [ ] Implement `LastRun`, `RunMode`, the staged swap, `RefreshAsync`, `CopyFrom` / `WhenIdle`, the events.
- [ ] XML doc grep on the new members.

## Acceptance criteria
- Recipe 2 common acceptance of [verification-recipe.md](verification-recipe.md) with `Squill.slnx` and `Squill.Windows.slnx` (both build Presentation); no head code, no AOT publish.
- A refresh re-runs the last refreshable run without prompt or history, keeps the old results on screen until the new ones are ready, then shows them with the same tab, filters, search, sort and (clamped) selection; a cancelled refresh changes nothing.

## Risks
- `ExecuteAsync` is the busiest method of the tab: keep `Run` behaviour byte-for-byte (the existing `QueryTabViewModelTests`, `QueryTabTransactionTests` and `QueryTabEditingTests` stay green unchanged).
- `AttachEditor` and `SetReadOnly` act on the staged results; they must not touch `Results` or `SelectedResult` before the swap.
- If `StreamingGridDataSource.StartAsync` turns out not to drain before returning (premise read at `ba7825f`), stop and report with a card fix.

## Suggested commit message
Refresh the last run of a query tab in place, keeping its results view
