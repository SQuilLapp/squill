# P20-28 - Avalonia: `n of m` badges, the filter popup, and removal of the old table dialog

Status: todo
Depends on: P20-26, P20-27

## Goal
Bring P20-27 to the Avalonia head (badge on connection, database and schema rows; DataGrip-style filter popup from the badge and the context menu), then delete the old "Tables to show" dialog everywhere, now that neither head uses it, and record the feature in the changelog and README.

Files today: `src/Squill.App.Avalonia/Views/ObjectExplorerView.axaml` (row `TreeDataTemplate`, `Grid ColumnDefinitions="Auto,*,Auto,Auto"`: column 2 environment `Border Classes="badge"`, column 3 `{Binding Trailing}`; context menu items `ChooseTablesItem`, `ShowAllItem`, `HideTableItem`, ...), `ObjectExplorerView.axaml.cs` (`OnNodeMenuOpening`: visibility per item name, default branch `_ => table`, so a new item **must** get its own case; `OnChooseTablesClicked`), `src/Squill.App.Avalonia/MainWindow.axaml.cs` line 105 (`Explorer.Picker = new Services.AvaloniaTablePicker(() => this)`), `src/Squill.App.Avalonia/Services/AvaloniaTablePicker.cs`, and the code-built flyout precedent `src/Squill.App.Avalonia/Views/ColumnMenu.cs`.

## Design decisions (final)
- **Badge** (`ObjectExplorerView.axaml`): column 2 becomes a horizontal `StackPanel` with the environment badge and a `Button Classes="filter-badge"` (`Content="{Binding FilterBadge}"`, `IsVisible="{Binding HasFilterBadge}"`, `Click="OnBadgeClicked"`, `ToolTip.Tip="{loc:Text explorer.chooseShown}"`, margin `6,0,0,0`). Style `Button.filter-badge` in the head's shared styles file (where the `badge` class lives): height 18, padding `5,0`, corner radius 3, 1 px `SquillBorderBrush`, transparent background, mono 11 px `SquillFg3Brush`, pointer-over background `SquillBgHoverBrush`, all `DynamicResource`. Same placement as WinUI (start of column 2).
- **Click**: `OnBadgeClicked` -> `Explorer.CreateFilter(node)` -> `ExplorerFilterMenu.Show(button, vm)`; the click must not change the tree selection (mark the event handled; check with the multi-select of P20-08).
- **Context menu**: replace `ChooseTablesItem` by `ChooseShownItem` (`{loc:Text explorer.chooseShown}`, `Click="OnChooseShownClicked"`), visible when `Explorer.CanFilter(node)` (add its case to the `switch`; connection rows included); it opens the popup anchored to the row. `ShowAllItem` and `HideTableItem` stay.
- **Popup** `src/Squill.App.Avalonia/Views/ExplorerFilterMenu.cs` (`internal sealed class`, one-line `<summary>`), the same layout as P20-27, built in code like `ColumnMenu.cs`: `Flyout` placement `BottomEdgeAlignedLeft`, width 440, list max height 420 in a `ScrollViewer`; top-right refresh / expand all / collapse all icon buttons (path icons of the head's icon resources, tooltips `explorer.refresh`, `explorer.filter.expandAll`, `explorer.filter.collapseAll`); search `TextBox` (`explorer.filter`); rows in a `StackPanel` rebuilt on `Rows.CollectionChanged`, each 26 px with indent `Depth * 20`, chevron, `CheckBox` (`Click` -> `ToggleCommand`, state from the row only), text (SemiBold when `IsStrong`), dim note, "add pattern" link turning into a `TextBox` (`explorer.filter.patternHint`; `Enter` commits, `Escape` cancels without closing the flyout), pattern rows with an `x` button, a 1 px line under the depth-0 `All` row; in-place updates from each row's `PropertyChanged`.
- **Removal** (nothing uses them after P20-27 and this card; grep to be sure before deleting):
  - `src/Squill.App.Avalonia/Services/AvaloniaTablePicker.cs` and `MainWindow.axaml.cs` line 105;
  - `src/Squill.Presentation/Explorer/ITableVisibilityPicker.cs` (`ITableVisibilityPicker`, `TableChoice`), `ExplorerTreeViewModel.Picker`, `ChooseTablesAsync` / `ChooseTablesCommand`;
  - tests `ChooseTables_HidesTheUnchecked_ShowsACount_AndSavesPerDatabase` and `CancelledPicker_ChangesNothing` with `FixedPicker` / `CancelPicker` in `ExplorerTreeViewModelTests.cs` (their behavior is covered by `ExplorerFilterViewModelTests` of P20-26: check a table toggle test saves per database before deleting them);
  - catalog keys `explorer.chooseTables`, `explorer.tablesDialog`, `explorer.selectAll`, `explorer.selectNone` (fr and en) when no other code uses them.
- **Docs**: `CHANGELOG.md` `[Unreleased]` "Added": the explorer filter popup (databases, schemas, tables, patterns, `n of m` badges; replaces the "Tables to show" dialog; saved per connection, old filters kept). `README.md` line 77: replace "The explorer can show only some tables ("Choose tables", saved per connection and database through `ITableFilterStore`)" with the new behavior.

## Out of scope
- View-model changes (go back to P20-26 if one is needed), WinUI (P20-27), system schemas, opening a database from the popup.

## Tasks
- [ ] Tests first, headless, `tests/Squill.App.Avalonia.Tests/ExplorerFilterPopupUiTests.cs` (fixture style of `ExplorerMultiSelectUiTests`: a SQLite file with tables `a`, `b` and view `v`): the database row shows the badge `3 of 3` (visible `Button.filter-badge`); opening the popup from the badge gives rows `All tables`, `a`, `b`, `v`; unchecking `a` removes it from the tree and the badge reads `2 of 3`; the context menu of the database shows `ChooseShownItem` and not the removed item; a column row shows no badge. The removals need no test: the build checks them.
- [ ] Implement the badge, the popup and the menu; then the removals and the docs.
- [ ] Publish the Avalonia AOT head and drive it with `Drive-Window.ps1` as in P20-27 (PostgreSQL `pg-visual` with `extra_db`, and SQLite `-Scenario Full`), dark and light, French and English; read the PNGs. Avalonia popups were invisible to the capture tools in P20-19: if the flyout is missing from the capture, write `not driven (popup not captured)` in the Done notes and list what the owner should look at; never claim it.
- [ ] Close the app and the container (`podman ps -a` empty), `dotnet build-server shutdown`.

## Acceptance criteria
- Recipe 2 common acceptance of [verification-recipe.md](verification-recipe.md) with `Squill.slnx` **and** `Squill.Windows.slnx` (the Presentation removals touch both heads), the Avalonia AOT publish (`eng/check-aot-warnings.ps1`), the app starts; catalog parity, used-keys and string scanner tests green after the key removals; captures read or recorded as not driven; everything closed.

## Risks
- The `_ => table` default of `OnNodeMenuOpening` shows any unknown item on table rows: the new item needs its own case.
- Rebuilding the explorer nodes on every toggle (P20-25) recreates the row the flyout is anchored to; the flyout must stay open (re-anchor to the explorer if Avalonia closes it, and say so).

## Suggested commit message
Show n of m badges and the explorer filter popup in the Avalonia head, remove the old table dialog
