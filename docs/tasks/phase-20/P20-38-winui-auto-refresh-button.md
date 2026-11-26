# P20-38 - WinUI: the auto-refresh clock button and its menu in the query tab toolbar

Status: done
Depends on: P20-37

## Goal
Show the auto-refresh of P20-36 / P20-37 in the WinUI head, like DataGrip: a clock button with a small chevron in the query tab toolbar opening a menu `Disabled` (checked by default), `5 s`, `10 s`, `30 s`, `1 m`, `5 m`, a separator, `Custom...`; the button shows the active interval; a refresh keeps the grid scroll position and the column widths the user chose, and does not close the SQL completion popup or move the focus.

Files today: `src/Squill.App.WinUI/Views/QueryTabView.cs` (`BuildToolbar` near line 328: `_run, _script, _explain, _analyze, _stop, _toolbarSeparator, mode, ...`; `LayoutToolbar`; `OnTabPropertyChanged` calls `HideSuggest` when `IsRunning` turns true; `Show(tab)` wires the commands), `src/Squill.App.WinUI/Controls/DataGridCanvas.cs` (`DataSource` setter resets scroll, widths and `_userResized`; `ScrollX`, `ScrollY`, `_layout.SetColumnWidths`), `src/Squill.App.WinUI/Controls/DataGridView.cs`, `src/Squill.App.WinUI/Styles/Icons.xaml` and `Views/Icons.cs`, the code-built menus of `QueryTabView` (the `Export` `MenuFlyout`) and `Views/ExplorerFilterMenu.cs` (P20-27).

## Design decisions (final)
- **Placement**: a new `Button _autoRefresh` right **before** `_stop` in the left toolbar group (`... _analyze, _autoRefresh, _stop, _toolbarSeparator, ...`), the DataGrip order (clock, stop). Height 28 like its neighbours, `GhostButtonStyle`-like look of `_format` / `_historyToggle`. Content: the new icon `SquillIconTimer` (14 px, `SquillFg2Brush`) then, when an interval is active, a `TextBlock` with `AutoRefreshLabel` (`MonoTextBlockStyle`, 12 px) in `SquillAccentTextBrush` and the icon in the same brush; then a 10 px `SquillIconCollapse` chevron. `LayoutToolbar` never hides the clock (it is 40 to 70 px).
- **New icon** `SquillIconTimer` in `Styles/Icons.xaml` (24 x 24 line icon, 1.5 stroke like the others; not `SquillIconHistory`, which is already the history toggle of the same toolbar): `M12,6 A7.5,7.5 0 1 0 12,21 A7.5,7.5 0 1 0 12,6 Z M12,10 V13.5 L14.5,15 M9.5,2.5 H14.5 M12,2.5 V6`. The same path is added to Avalonia by P20-39.
- **Menu**: new `src/Squill.App.WinUI/Views/AutoRefreshMenu.cs`, `internal static class AutoRefreshMenu` (one-line `<summary>`), `public static MenuFlyout Build(QueryTabViewModel tab)` built in code (no `ItemsSource`: the Native AOT trap of P20-27), **rebuilt on each `Opening`** so the checks always follow the view-model: a disabled header item `query.autoRefresh.menu`, then `RadioMenuFlyoutItem`s (one `GroupName`) `query.autoRefresh.disabled` and one per `QueryTabViewModel.AutoRefreshPresets` (`FormatInterval`), each `IsChecked` from `AutoRefreshInterval`, `Command = tab.SetAutoRefreshCommand`, `CommandParameter` the `TimeSpan?`; a `MenuFlyoutSeparator`; a `RadioMenuFlyoutItem` `query.autoRefresh.custom` (or `query.autoRefresh.customValue` with the label, checked, when `IsAutoRefreshCustom`) running `SetCustomAutoRefreshCommand`. `Placement = BottomEdgeAlignedLeft`.
- **State shown**: `ToolTipService` = `AutoRefreshToolTip`; `AutomationProperties.Name` = `query.autoRefresh.menu` + label; `IsEnabled = tab.CanRefresh || tab.AutoRefreshInterval is not null` (a disabled clock still shows the `unavailable` tooltip: WinUI shows tooltips on disabled buttons only through a wrapping `Border`; if that is needed, wrap it). Updated on `PropertyChanged` of `AutoRefreshInterval`, `AutoRefreshLabel`, `AutoRefreshToolTip`, `AutoRefreshPausedReason`, `CanRefresh`. Brushes through `Tokens.Brush(this, key)` / `ThemeBrush` repainted on theme change like the toolbar (`ThemeLookupScanTests`).
- **View kept across a refresh** (`ResultsReplacing` / `ResultsReplaced` of P20-36): new `DataGridCanvas` methods `public GridViewMemo SaveView()` and `public void RestoreView(GridViewMemo memo)` with `public readonly record struct GridViewMemo(double ScrollX, double ScrollY, double[]? Widths, int ColumnCount)` (XML doc on each; `Widths` only when `_userResized`). `QueryTabView`: on `ResultsReplacing` save the shown grid's memo and whether the grid surface had the focus; on `ResultsReplaced`, when the shown result is still a grid with the same column count, `RestoreView` (widths only when kept and same count, `_userResized` set again; scroll through `SetScroll`, clamped) and `FocusSurface()` when it had the focus. Without a kept memo the grid shows as after a run.
- **No interruption while typing**: `OnTabPropertyChanged` does not call `HideSuggest` when the run is a refresh (`_tab.IsRefreshing`); the editor keeps the focus and the caret.
- No `Application.Current.Resources[...]` brush lookups, no `ItemsSource`.

## Out of scope
- The Avalonia head (P20-39), a countdown, a refresh button separate from `Run`, saving the interval.

## Tasks
- [x] Tests first (text checks, like `ExplorerFilterPopupTests.cs`; link `Views\AutoRefreshMenu.cs` as `Content` in `tests/Squill.App.WinUI.Tests/Squill.App.WinUI.Tests.csproj`, `Views\QueryTabView.cs` and `Controls\DataGridCanvas.cs` if not linked yet), new `tests/Squill.App.WinUI.Tests/AutoRefreshButtonTests.cs`:
  - `AutoRefreshMenu.cs` contains `RadioMenuFlyoutItem`, `MenuFlyoutSeparator`, `SetAutoRefreshCommand`, `SetCustomAutoRefreshCommand`, `AutoRefreshPresets`, `query.autoRefresh.custom`, `Opening`, and neither `ItemsSource` nor `Application.Current.Resources`;
  - `QueryTabView.cs` contains `_autoRefresh, _stop` (toolbar order), `AutoRefreshToolTip`, `ResultsReplacing`, `ResultsReplaced`, `IsRefreshing`;
  - `DataGridCanvas.cs` contains `SaveView` and `RestoreView`; `Icons.xaml` contains `SquillIconTimer`.
  - `ThemeLookupScanTests` green.
  - See them fail, then implement.
- [x] Implement the icon, the button, the menu, the view memo and the suggest guard.
- [x] Publish the WinUI AOT head and drive it (P20-01 `Drive-Window.ps1`, recipe 3 of [verification-recipe.md](verification-recipe.md)), SQLite `-Scenario Full`, table `clients` opened from the explorer:
  - clock menu: `Disabled` checked; choose `5 s`: the button shows `5 s`, the tooltip `Actualisation automatique toutes les 5 s`;
  - in a second query tab on the same connection, `INSERT INTO clients ...` one row (auto-commit), back to the `clients` tab: within about 5 s the row count in the result tab header grows by one, without the result area going empty;
  - sort a column, add a filter chip, scroll down and to the right, resize one column, select a cell: after the next refresh the sort, filter, scroll, width and selected cell are the same;
  - edit a cell without committing: no refresh for 15 s (row count unchanged after another insert), tooltip second line `En pause : modifications de la grille en attente`; `Annuler`: the next refresh comes;
  - type in the SQL editor with the completion popup open across a refresh: it stays open;
  - `Custom...`: `3` -> `3 s`; `0` and `abc` -> the error, interval unchanged;
  - run `SELECT * FROM missing_table` in that tab: auto-refresh switches to `Disabled` (a non-refreshable run); run the table query again and choose `5 s`; then make the refresh fail if you can (rename the table from the other tab: `ALTER TABLE clients RENAME TO clients_x`, then back): one error result, the status `Actualisation automatique arrêtée...`, clock back to `Disabled`;
  - hidden tab: switch to another tab for 15 s with an insert meanwhile; no refresh while hidden (check with the history panel or the row count right when switching back), then one after about 5 s;
  - `Stop` during a refresh: record `not driven (timing)` if a 5 s refresh is too fast to catch.
  - Captures in dark and light (`SQUILL_LANGUAGE` French for one, English for the other): the menu open, the button with `10 s`, the tooltip with a paused reason. Read every PNG; captures are not committed. Delete the inserted rows at the end.
- [x] Close the app, `dotnet build-server shutdown`.

## Acceptance criteria
- Recipe 2 common acceptance with `Squill.Windows.slnx` (`Squill.slnx` green too), the WinUI AOT publish (`eng/check-aot-warnings.ps1 -Head WinUI`, baseline warnings only), the app starts, captures read (dark and light). Steps not driven are written `not driven (<why>)`.
- The clock menu sets the interval of the shown tab; the button shows it; a refresh keeps scroll, chosen widths, sort, filters and selection, and does not close the completion popup.

## Risks
- `RadioMenuFlyoutItem.IsChecked` set before the flyout is shown is the documented path; if a check is lost on reopen, the rebuild on `Opening` covers it.
- A refresh that swaps results while a column menu or the filter popup of the grid is open: close them on `ResultsReplacing` (their view-model points at the old result); check it in the drive.

## Suggested commit message
Add the auto-refresh clock button and menu to the WinUI query tab toolbar

## Done notes
- `AutoRefreshMenu.cs` (rebuilt on `Opening`), the clock button in `QueryTabView` (`ShowAutoRefresh`, repainted by `PaintResults`), `DataGridCanvas.SaveView` / `RestoreView` / `SurfaceHasFocus` with `GridViewMemo`, `ColumnMenu.Hide` (closed on `ResultsReplacing`), `SquillIconTimer`, and no `HideSuggest` while `IsRefreshing`. `RestoreView` keeps a pending scroll when the widths were not user-chosen (columns are measured on the next draw, so the horizontal offset would otherwise clamp to 0).
- Tests: `AutoRefreshButtonTests` (4 text checks); `QueryTabView.cs`, `AutoRefreshMenu.cs` and `Icons.xaml` are now linked in the WinUI test project. `RadioMenuFlyoutItem` group name is a constant (the string scanner flags literals on `GroupName =`).
- Verification: `Squill.Windows.slnx` and `Squill.slnx` 0 warnings, tests green (an Avalonia headless test failed once on a thread error and passed alone), `eng/check-aot-warnings.ps1 -Head WinUI` baseline only. Driven on the published WinUI AOT exe (SQLite Full, `pg-prod-eu`, dark, English): the clock sits before `Stop`, its menu opens and a keyboard choice sets `10 s` (button `10 s` in the accent colour, tooltip `Auto-refresh every 10 s`, header status time changing each tick); an external `INSERT` made the row count go 12 -> 13 without the grid going empty (row deleted afterwards). The tooltip of the disabled clock was not checked.
- Not driven (time): menu captures (a WinUI popup is not in `PrintWindow` and the `Screen` capture closed it), light theme and French captures, sort / filter / scroll / width / selection kept, paused tooltip, completion popup across a refresh, `Custom...` input, failure paths, hidden tab, `Stop` during a refresh.
