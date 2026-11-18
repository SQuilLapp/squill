# P18-34 - Avalonia status bar: refresh only on the UI thread

Status: todo
Depends on: P18-15, P18-31

## Goal
`StatusBar.Refresh()` (`src/Squill.App.Avalonia/Views/StatusBar.axaml.cs:150`) touches controls (`RefreshEnvironment`, `ConnectionText`, `RefreshTransaction`, ...) and is called from `PropertyChanged` handlers (`OnWorkspaceChanged`, `OnTabChanged`) and from the `Show*` methods. Some of those events are raised on a background thread, so the head sometimes throws an unhandled `InvalidOperationException` ("Call from invalid thread", at `RefreshEnvironment`, line ~170). Found by P18-31: `LayoutOverlapTests` fails about one run in two with it. A user can hit the same crash when a connection or monitor read finishes on a worker thread.

## Design decisions (final)
- Fix at the entry points: `Refresh()` starts with `if (!Dispatcher.UIThread.CheckAccess()) { Dispatcher.UIThread.Post(Refresh); return; }` (`global::Avalonia.Threading`). `OnCaretChanged` calls `RefreshPosition` directly and gets the same guard. Do not wrap individual controls. Never use `Dispatcher.UIThread.Invoke` (deadlock risk).
- Before fixing, find who raises the event off-thread (read the stack of the unhandled exception from a failing `LayoutOverlapTests` run) and record it in the card's Done notes. If the cause is a view-model that should raise on the UI thread, do **not** change Presentation in this card: keep the head-side guard and list the cause as a follow-up.
- Several posted refreshes are harmless (`Refresh` is idempotent): no queue, no flag.
- Other Avalonia views with the same pattern are out of scope; list any you notice.

## Tasks
- [ ] Guard `Refresh` and `OnCaretChanged` as above.
- [ ] Test in `tests/Squill.App.Avalonia.Tests` (`[Collection("Headless")]`, next to the existing status bar tests or in a new `StatusBarThreadTests`): build the `StatusBar` with a workspace, raise the `PropertyChanged` it listens to from `Task.Run`, flush the dispatcher (`Dispatcher.UIThread.RunJobs()`), assert no exception and that the environment block reflects the new value. Run it once without the guard to prove it fails, then restore the guard.
- [ ] Stability proof: `rtk bypass dotnet test tests/Squill.App.Avalonia.Tests -c Release --filter "FullyQualifiedName~LayoutOverlapTests"` five times in a row, all green.
- [ ] Mark the item fixed in `docs/verification/new-screens.md` (Bugs found).

## Acceptance criteria
- Common acceptance (recipe 7) with the Avalonia AOT publish (`rtk dotnet publish src/Squill.App.Avalonia -c Release -r win-x64`, zero IL2xxx/IL3xxx warnings, the app starts); everything started is closed.
- No behavior change on the UI thread (the guard is a no-op there).

## Risks
- A posted refresh can run after the tab is detached: `Refresh` already null-checks `_compare`, `_monitor`, `_security`, `_plan` and `_workspace`; keep it that way.
- A flaky test is not proof: the five consecutive green runs are required.

## Suggested commit message
Refresh the Avalonia status bar on the UI thread only
