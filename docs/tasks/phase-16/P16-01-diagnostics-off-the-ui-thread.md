# P16-01 - Presentation: SQL diagnostics debounced and computed off the UI thread

Status: todo
Depends on: none

## Goal
Typing in a query tab must never wait for the diagnostics. Today `QueryTabViewModel` runs `SqlDiagnosticsProvider.GetDiagnostics` on the whole text, synchronously on the UI thread, at every `TextChanged` (`UpdateDiagnostics`, `src/Squill.Presentation/Query/QueryTabViewModel.cs`); `docs/poc/editor.md` already predicted it "will stall on such scripts", and `SqlDiagnosticsProvider` carries a `shortcut:` note (scope resolved per column reference, re-tokenizing the text).

## Design decisions (final)
- Measure first. Generate a script of 5,000 lines (about 1,500 statements, joins and aliases, against a `SchemaSnapshot` of 200 tables) in a test helper, time `GetDiagnostics` in `Release` (a throw-away console in the scratchpad or `tools/Squill.Bench`, never a timing assertion in a unit test) and write the figure in the card's report. If it is above 150 ms, make `SqlDiagnosticsProvider` resolve the scope once per statement instead of once per column reference (remove the `shortcut:` comment, results must be identical: `SqlDiagnosticsProviderTests` stay unchanged and green); if it is below, leave the provider alone and keep the comment.
- `QueryTabViewModel.UpdateDiagnostics` becomes: read `_editor.Text`, `CaretOffset`, dialect and schema on the calling (UI) thread, start a 250 ms delay on the tab's `TimeProvider` (`_time`), cancel the previous pending run (one `CancellationTokenSource` field, disposed on replace and on tab close), compute on `Task.Run`, and apply through `IUiDispatcher` only if the text version is still the latest (an `int` counter incremented in the `TextChanged` handler). The constructor's first call and the call after a schema refresh stay immediate (no delay) so a freshly opened tab and a refreshed schema show their markers at once.
- `SetDiagnostics` keeps its contract; `ISqlEditor` does not change; both heads and the fakes are untouched. Errors in the computation are swallowed after clearing the markers (diagnostics are a convenience, `GetDiagnostics` already never throws).
- No change to completion, hover or the plan state refresh in the same handler.

## Out of scope
- Incremental parsing, diagnostics for a selection only, changing any diagnostic text or code.

## Tasks
- [ ] Measurement recorded in the card report (and the provider change only if the rule above says so).
- [ ] Debounce, cancellation, stale-result drop and dispatcher marshalling in `QueryTabViewModel`.
- [ ] Tests in `tests/Squill.Presentation.Tests/Query/`: ten quick `TextChanged` raises produce one `SetDiagnostics` call after the delay (with the fake time provider the tests already use; grep for it); an older result never overwrites a newer one (gate the first computation with a `TaskCompletionSource`); closing the tab cancels a pending run; the first call and a schema refresh apply without waiting for the delay.

## Acceptance criteria
- Common acceptance (build 0 warnings, all tests green). Both heads still compile and their tests pass (`Squill.Windows.slnx` as the README says for the WinUI head).

## Suggested commit message
Compute SQL diagnostics off the UI thread, debounced
