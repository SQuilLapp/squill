# P20-08 - Avalonia: select several tables in the explorer and drop them

Status: todo
Depends on: P20-06, P20-07

## Goal
Same as P20-07 for `src/Squill.App.Avalonia/Views/ObjectExplorerView.axaml` (`TreeView Name="Tree"`, `SelectedItem="{Binding SelectedNode, Mode=TwoWay}"`) and `ObjectExplorerView.axaml.cs` (`OnNodeMenuOpening` sets `Tree.SelectedItem = node`, `OnDropClicked`, `OnTreeKeyDown`). Read the Done notes of P20-07 first (same behavior, same texts).

## Design decisions (final)
- `TreeView SelectionMode="Multiple"` (Avalonia supports Ctrl / Shift selection and `SelectedItems`); keep the `SelectedItem` binding. Subscribe to `Tree.SelectionChanged` and call `Explorer.SetSelectedNodes(Tree.SelectedItems.OfType<ExplorerNode>())`.
- `OnNodeMenuOpening`: do not reassign `Tree.SelectedItem` when the node is already in `Tree.SelectedItems`; `DropItem.Header` = `Explorer.DropMenuText(node)`, `DropItem.IsEnabled` = `Explorer.CanDropMenu(node)`; `DesignItem` and the script item disabled while more than one table is selected.
- `Delete` in `OnTreeKeyDown` executes `DropSelectedTablesCommand` when it can, handled.
- Headless tests in `tests/Squill.App.Avalonia.Tests/ExplorerMultiSelectUiTests.cs` (style of `ExplorerAndTabsTests.cs`, `Headless.cs`): select two table nodes by `Tree.SelectedItems.Add`, assert `Explorer.SelectedTableCount == 2`; open the node menu (`OpenMenu`, line 43 of the view) and assert the drop item header and enabled state; select a table and a view: disabled; `Delete` raises `DropTablesRequested` once. They fail first.

## Out of scope
- WinUI, view-model logic, Linux / macOS key differences (`Delete` is the same; record that macOS `Cmd+Backspace` was not added).

## Tasks
- [ ] Tests first, then the view changes.
- [ ] Publish the Avalonia AOT head and drive it like P20-07 (Ctrl+click two tables, menu text, `Delete`, cancel); read a capture in light and dark (scale 1.5: pass the pixel size, see P19-14).

## Acceptance criteria
- Recipe 2 common acceptance (`Squill.slnx`, Avalonia AOT publish, app starts, `LayoutOverlapTests` green); capture read; processes closed.

## Risks
- Avalonia `TreeView` multi-select with `SelectedItem` two-way binding: setting `SelectedNode` from the view-model must not clear the multi-selection; test with a programmatic `SelectedNode` change after a two-table selection.

## Suggested commit message
Select and drop several tables in the Avalonia explorer
