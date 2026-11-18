# P19-02 - Presentation: raise view-model events on the UI thread

Status: todo
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
- [ ] Inventory table in the Done notes.
- [ ] For each finding: a test that raises from `Task.Run` and asserts the event arrives through the dispatcher (red first), then the fix.
- [ ] Re-run `Squill.App.Avalonia.Tests` `LayoutOverlapTests` and `StatusBarThreadTests` three times, green.

## Acceptance criteria
- Common acceptance (recipe 4) with both AOT publishes only if a head file changed; Presentation behavior on the UI thread is identical (existing tests unchanged and green).

## Risks
- Posting makes a raise asynchronous: a caller that reads the property immediately after setting it is fine (the value is set synchronously), but a caller that waits for the event must be found in the inventory.
- Do not touch `StatusBar.Refresh` guards; they stay as defence.

## Suggested commit message
Raise Presentation events through the UI dispatcher where they can run on a worker
