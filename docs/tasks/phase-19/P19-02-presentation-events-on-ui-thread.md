# P19-02 - Presentation: raise view-model events on the UI thread

Status: done
Depends on: none

## Goal
P18-34 fixed `StatusBar.Refresh` in Avalonia with a head-side guard and recorded the cause in its Done notes: `WorkspaceViewModel.ShowTab` and `OnActiveSessionChanged` raise `PropertyChanged(ActiveEnvironment)` on whatever thread calls them, and "the other Avalonia views that subscribe to workspace / view-model events and touch controls directly were not audited". Avalonia has about 45 `PropertyChanged +=` subscriptions in `src/Squill.App.Avalonia` (`rtk grep -c "PropertyChanged +=" -r src/Squill.App.Avalonia --include=*.cs`), WinUI has a similar number, and WinUI throws `COMException` / `RPC_E_WRONG_THREAD` for the same mistake. Find which Presentation code can raise off the UI thread in production, and fix it at the source.

## Design decisions (final)
- Step 1 is an inventory, not a fix. In `src/Squill.Presentation` list every place that raises `PropertyChanged`, `CollectionChanged` or a custom event after an `await ... ConfigureAwait(false)`, inside `Task.Run`, in a timer callback (`TimeProvider.CreateTimer`, `System.Threading.Timer`) or in a callback handed to a driver / tunnel / update service. Write the table (file, member, thread it can run on, who calls it) in the card's Done notes. Heads run a `SynchronizationContext`, so a plain `await` without `ConfigureAwait(false)` returns to the UI thread and is not a finding.
- Fix at the producer: the object that owns the state and has (or can get) an `IUiDispatcher` posts the raise through it (`_dispatcher.Post(...)`, as `WorkspaceViewModel` already does for `RefreshElapsed`). Never `Invoke` (deadlock risk). A raiser that is only reachable from the UI thread is left alone.
- `ShowTab` and `OnActiveSessionChanged` are public API used by heads and tests: keep them synchronous when called on the UI thread. `ImmediateDispatcher` (tests) keeps tests simple; add a test dispatcher that records the thread only if no existing one does.
- If the inventory finds nothing reachable in production (only tests call off-thread), stop: put the table in the Done notes, mark the card done with "no production producer found; head guard of P18-34 stays", change no code.

## Out of scope
- New guards in heads beyond what the inventory proves; rewriting view-models to marshal everything.

## Tasks
- [x] Inventory table in the Done notes.
- [x] (no finding, nothing to do) For each finding: a test that raises from `Task.Run` and asserts the event arrives through the dispatcher (red first), then the fix.
- [x] (no code changed, nothing to re-run) Re-run `Squill.App.Avalonia.Tests` `LayoutOverlapTests` and `StatusBarThreadTests` three times, green.

## Acceptance criteria
- Common acceptance (recipe 4) with both AOT publishes only if a head file changed; Presentation behavior on the UI thread is identical (existing tests unchanged and green).

## Risks
- Posting makes a raise asynchronous: a caller that reads the property immediately after setting it is fine (the value is set synchronously), but a caller that waits for the event must be found in the inventory.
- Do not touch `StatusBar.Refresh` guards; they stay as defence.

## Suggested commit message
Raise Presentation events through the UI dispatcher where they can run on a worker

## Done notes

No production producer found; the head guard of P18-34 stays. No code changed.

Inventory of `src/Squill.Presentation` (every `Task.Run`, `ConfigureAwait(false)`, timer and driver callback):

| File | Member | Thread it can run on | Raises how | Verdict |
|---|---|---|---|---|
| `Activity/SessionsMonitorViewModel.cs` | timer callback (line 121) | timer pool thread | `_dispatcher.Post(Tick)` | already posted |
| `Activity/SessionsMonitorViewModel.cs` | `Task.Run` read (line 248) | worker, then `await` resumes on the UI context | none on the worker | not a finding |
| `Workspace/WorkspaceViewModel.cs` | `_elapsedTimer` (line 103) | timer pool thread | `_dispatcher.Post(RefreshElapsed)` | already posted |
| `Compare/SchemaCompareViewModel.cs` | `Task.Run` reads, compare, script build | worker, plain `await` back | none on the worker | not a finding |
| `Palette/PaletteObjectProvider.cs`, `PaletteViewModel.cs` | `Task.Run` search and index | worker, plain `await` back | none on the worker | not a finding |
| `Query/QueryTabViewModel.cs` | `UpdateDiagnosticsDebounced` (line 689) | worker | `_dispatcher.Post(...)` for `SetDiagnostics` | already posted |
| `Results/ColumnMenuViewModel.cs` | `LoadAsync` (`ConfigureAwait(false)`) | worker | all state changes inside `dispatcher.Post` | already posted |
| `Results/FilteredGridDataSource.cs` | `RunAsync` (`Task.Run`) | worker | `RaiseChanged` and `OnInnerChanged` use `_dispatcher.Post` | already posted |
| `Results/StreamingGridDataSource.cs` | `StartAsync` | UI context (no `ConfigureAwait(false)`) | `RaiseChanged` = `_dispatcher.Post` | already posted |
| `Import/ImportWizardViewModel.cs` | `Sink` (`IProgress`) | worker of the runner | `_dispatcher.Post(() => Apply(p))` | already posted |
| `Import/CsvPreview.cs`, `CsvReader.cs`, `Results/*Exporter.cs`, `ResultFileWriter.cs` | `ConfigureAwait(false)` | worker | no event, no property raised | not a finding |
| `Localization/LanguageSetup.cs` | `Task.Run(...).GetResult()` | blocks the caller | none | not a finding |
| `Sessions/SessionTransaction.cs`, `Sessions/SessionManager.cs`, `Workspace/WorkspaceViewModel.cs` (`ShowTab`, `OnActiveSessionChanged`) | setters | only from commands, `Explorer` events and awaits that resume on the UI context | direct raise | UI thread only |

The cause recorded by P18-34 (`ShowTab` / `OnActiveSessionChanged` raising on the caller's thread) needs a non-UI caller; none exists in Presentation or in the heads (`ActiveSession =` and `ShowTab` are only reached from UI-thread handlers and plain awaits). No `ConfigureAwait(false)` precedes a property change anywhere in Presentation. The Avalonia and WinUI head subscriptions were not audited one by one: the inventory shows nothing for them to receive off-thread.
