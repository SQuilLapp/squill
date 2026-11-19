# P20-07 - WinUI: select several tables in the explorer and drop them

Status: todo
Depends on: P20-06

## Goal
The WinUI explorer tree (`src/Squill.App.WinUI/Views/SidebarView.xaml`, line 40: `TreeView x:Name="Tree" SelectionMode="Single"`; `SidebarView.xaml.cs`: `OnTreeSelectionChanged`, `OnTreeKeyDown`, `OnNodeContextRequested`) selects one node. Let the user select several tables and drop them in one action (recipe 4 of [verification-recipe.md](verification-recipe.md); the logic is P20-05 and P20-06, this card only feeds the selection and shows the action).

## Design decisions (final)
- Step 1, a spike before any commit: set `SelectionMode="Multiple"` and check in the running head (P20-01 `Drive-Window.ps1`, SQLite `-Scenario Full`, connection `local-sqlite` expanded) that (a) `Tree.SelectedItems` holds the Ctrl / Shift-clicked nodes, (b) the `SelectionChanged` event fires, (c) a single click, double tap (`OnTreeDoubleTapped` opens a table), `Enter` on a connection and the context menu still reach the right node, (d) what a multi-select `TreeViewItem` looks like (WinUI 3 draws a selection checkbox in `Multiple` mode). Write the result in the Done notes. **Stop rule:** if (c) cannot be kept without a rewrite of the handlers, or the checkboxes cannot be hidden with the `ItemContainerStyle` / template already in `SidebarView.xaml` and look wrong next to `M03`, stop and report; the proposed card fix is a "Supprimer des tables..." picker dialog opened from the database node, built like `ITableVisibilityPicker` (`ChooseTablesCommand`).
- With the spike passing: `SelectionMode="Multiple"`; `OnTreeSelectionChanged` calls `_workspace.Explorer.SetSelectedNodes(Tree.SelectedItems.OfType<ExplorerNode>())` and keeps `Explorer.SelectedNode` equal to the last clicked node; double tap and `Enter` act on that node only.
- `Delete` key in `OnTreeKeyDown`: executes `Explorer.DropSelectedTablesCommand` when `CanExecute`, marks the event handled.
- Context menu (`OnNodeContextRequested`, table branch): `Tree.SelectedItem = node` is only done when the node is **not** already in the selection (a right click inside a multi-selection must keep it); the drop item text is `explorer.DropMenuText(node)`, enabled by `explorer.CanDropMenu(node)`; `Design`, `Script CREATE TABLE` are disabled (not hidden) while more than one table is selected.
- No new strings beyond P20-06's. Both themes: the selection fill is the existing tree selection style; check it in light and dark.

## Out of scope
- The Avalonia tree (P20-08), changing the view-model.

## Tasks
- [ ] Spike, decision in the Done notes (continue or stop).
- [ ] Tests first in `tests/Squill.App.WinUI.Tests/ExplorerMultiSelectTests.cs` in the style of `ExplorerAndTabsTests.Explorer_tree_rows_are_24_high` (text checks of `Xaml/SidebarView.xaml`: `SelectionMode="Multiple"`, a `KeyDown` handler) plus a test of any pure helper split out of the handler (selection to node list); see them fail, then implement.
- [ ] Publish the AOT head, drive: select two tables with Ctrl+click, right click inside the selection (menu says `Supprimer 2 tables`), `Delete` key, cancel at the confirmation; read a capture of the selected tree and of the menu (light and dark).

## Acceptance criteria
- Recipe 2 common acceptance with `Squill.Windows.slnx` and the WinUI AOT publish (`eng/check-aot-warnings.ps1`); the app starts; the capture was read; processes closed.

## Risks
- `TreeView` multi-select with a custom `ItemTemplate` that has its own `TreeViewItem` (line 50 of the XAML) is the part most likely to misbehave; the spike exists for that.
- Keys: `Delete` must not fire while the explorer filter box has the focus (check the handler is on the tree).

## Suggested commit message
Select and drop several tables in the WinUI explorer
