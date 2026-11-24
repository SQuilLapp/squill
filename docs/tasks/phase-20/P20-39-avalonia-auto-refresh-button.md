# P20-39 - Avalonia: the auto-refresh clock button and its menu in the query tab toolbar

Status: todo
Depends on: P20-37, P20-38

## Goal
Bring P20-38 to the Avalonia head: the clock button before `Stop` with the `Disabled` / `5 s` / `10 s` / `30 s` / `1 m` / `5 m` / `Custom...` menu, the active interval on the button and in its tooltip, the grid scroll and chosen column widths kept across a refresh, the completion popup left open. Then write the README and changelog lines of the feature.

Files today: `src/Squill.App.Avalonia/Views/QueryTabView.axaml` (`ToolbarLeft`: `RunButton`, `ScriptButton`, `ExplainButton`, `AnalyzeButton`, `StopButton` near line 84, `TxSeparator`, ...) and `.axaml.cs` (`OnTabPropertyChanged`), `src/Squill.App.Avalonia/Controls/DataGridCanvas.cs` (`ScrollX`, `ScrollY`, the `DataSource` reset), `src/Squill.App.Avalonia/Styles/Icons.axaml`, the code-built `Flyout` of `Views/ExplorerFilterMenu.cs` (P20-28).

## Design decisions (final)
- Same placement, content, states, tooltip, enabled rule, view memo (`SaveView` / `RestoreView`, `GridViewMemo`, focus kept), suggest guard and keys as P20-38; only the Avalonia mechanics differ:
  - `SquillIconTimer` added to `Styles/Icons.axaml` as a `StreamGeometry` with the path of P20-38.
  - `Button Name="AutoRefreshButton"` declared in the axaml right before `StopButton` (icon `Path Classes="icon"`, a `TextBlock Name="AutoRefreshLabel" Classes="mono"` visible when an interval is active, the chevron), `ToolTip.Tip` set from code on `PropertyChanged`; brushes as `DynamicResource` like the toolbar.
  - Menu: new `src/Squill.App.Avalonia/Views/AutoRefreshMenu.cs`, `internal static class AutoRefreshMenu` (one-line `<summary>`), `public static MenuFlyout Build(QueryTabViewModel tab)` built in code, rebuilt each time the button is clicked (no `ItemsSource`): a disabled header `MenuItem`, `MenuItem`s with `ToggleType = MenuItemToggleType.Radio`, one `GroupName`, `IsChecked` from `AutoRefreshInterval`, `Command` / `CommandParameter` as WinUI, a `Separator`, the `Custom...` item. Shown with `ShowAt(AutoRefreshButton)`.
  - If `MenuItemToggleType.Radio` does not exist in the Avalonia version of `Directory.Packages.props`, use `MenuItem.Icon` with `SquillIconCheck` on the checked item instead (same items, same order) and say so in the Done notes.
- Popups and flyouts are invisible to the capture tools (P20-19): the open menu may be `not driven (capture)`; the button label, tooltip-free states and the grid behaviour are captured.

## Out of scope
- The WinUI head (P20-38), a countdown, saving the interval.

## Tasks
- [ ] Tests first, `tests/Squill.App.Avalonia.Tests/AutoRefreshButtonUiTests.cs` (headless, like `EditorToolbarTests.cs` / `ExplorerFilterPopupUiTests.cs`):
  - the toolbar holds `AutoRefreshButton` right before `StopButton`;
  - `AutoRefreshMenu.Build(tab)` has, in order, the header, `Disabled` checked, the five presets, a `Separator`, `Custom...`; invoking the `10 s` item sets `tab.AutoRefreshInterval` to 10 s (after a refreshable run in the fake session) and the label shows `10 s`;
  - with `IsAutoRefreshCustom`, the last item reads `Personnalisé (90 s)...` (French) and is checked;
  - the button is disabled with the `unavailable` tooltip before any run;
  - `DataGridCanvas.SaveView` then `RestoreView` after a new `DataSource` with the same columns brings back `ScrollY` (clamped) and user widths;
  - `LocalizationUiTests` / catalog tests green.
  - See them fail, then implement.
- [ ] Implement the icon, the button, the menu, the view memo, the suggest guard.
- [ ] Publish the Avalonia AOT head and drive it as P20-38 (same data, same steps); record the menu as `not driven (capture)` when it cannot be seen, and check the chosen interval through the button label instead.
  - Captures in dark and light (French for one, English for the other): the button with `10 s`, the grid after a refresh with the kept sort / scroll. Read every PNG; captures are not committed. Delete the inserted rows at the end.
- [ ] Update the README phase 20 section (a line for P20-36 ... P20-39) and add a line to `CHANGELOG.md` under the unreleased section (`Auto-refresh of a query tab: Disabled, 5 s ... 5 m or a custom interval; pauses on pending edits; stops on error`), if the phase 20 changelog section exists (P20-28 wrote one).
- [ ] Close the app, `dotnet build-server shutdown`.

## Acceptance criteria
- Recipe 2 common acceptance of [verification-recipe.md](verification-recipe.md) with `Squill.slnx`, the Avalonia AOT publish (`eng/check-aot-warnings.ps1 -Head Avalonia`, baseline warnings only), the app starts, captures read (dark and light). Steps not driven are written `not driven (<why>)`.
- Same behaviour as P20-38 in the Avalonia head.

## Risks
- A headless `MenuFlyout` may not build its items until shown: test `AutoRefreshMenu.Build` on the returned item list, not on the visual tree.
- `ToolTip.Tip` on a disabled button is not shown by Avalonia unless `ToolTip.ShowOnDisabled="True"`; set it.

## Suggested commit message
Add the auto-refresh clock button and menu to the Avalonia query tab toolbar
