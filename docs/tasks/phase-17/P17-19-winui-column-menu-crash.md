# P17-19 - WinUI column menu crash and crash log

Status: done
Depends on: P17-18

## Goal
The owner's hand test of the WinUI Release build crashed the app when clicking the chevron of a column header after a query (`Microsoft.UI.Xaml.dll`, `0xc000027b`, a stowed XAML exception with no managed details). Make the menu robust and make the next such crash diagnosable.

## Findings
- Not proved: the managed exception is lost, so the exact throw site was not observed (no click automation in the fixing session). Any managed exception leaving a XAML callback surfaces as this stowed exception.
- Mitigated, in `Views/ColumnMenu.cs`: the value list was a `ListView` fed an `IReadOnlyList<ValueItem>` (a non-WinRT collection marshalled as `ItemsSource`) whose container `Content` was overwritten in `ContainerContentChanging`; the same `Flyout` instance was reused; `ShowAt` had no `XamlRoot` guard. Now: plain `CheckBox` rows in a `StackPanel` inside a `ScrollViewer` (no `ItemsSource`, no containers), a new `Flyout` per show, and `Show` returns (disposing the view-model) when the grid has no `XamlRoot`.
- Checked and fine: every style, icon and token key the menu reads exists in both themes.

## Design decisions (final)
- `CrashLog` (WinUI head): appends time, source, and type, message and stack of the exception and each inner one to `%APPDATA%\SQuilL\logs\crash.log` (`SQUILL_DATA_DIR\logs\crash.log` when set). Hooked to `Application.UnhandledException` (`Handled` untouched), `AppDomain.UnhandledException` and `TaskScheduler.UnobservedTaskException`. It never throws.
- Space on a value row is the native `CheckBox` behaviour; Enter still applies, Escape closes.

## Tasks
- [x] `CrashLog` and its hook in `App`, `CrashLogTests` (format).
- [x] Rebuild the value list of `ColumnMenu` without `ListView`; fresh flyout per show; `XamlRoot` guard.

## Acceptance criteria
- Builds with 0 warnings, tests green, AOT publish of the WinUI head starts.
- Owner re-test: run a query, click a header chevron, in light and dark; if it still crashes, read `crash.log`.

## Suggested commit message
Fix the WinUI column menu crash and add a crash log
