# P20-06 - Presentation: the explorer selection of several tables and the drop command

Status: done
Depends on: P20-05

## Goal
The explorer view-model knows one selected node (`SelectedNode`) and raises `DropTableRequested` for one table. Give it a selection of several nodes that the heads feed, and a command and event that ask the workspace to drop all the selected tables (recipe 4 of [verification-recipe.md](verification-recipe.md)).

## Design decisions (final)
- `src/Squill.Presentation/Explorer/ExplorerTreeViewModel.cs`:
  - `public IReadOnlyList<ExplorerNode> SelectedNodes { get; }` and `public void SetSelectedNodes(IEnumerable<ExplorerNode> nodes)` (called by the heads on every selection change; stores a copy; raises `PropertyChanged` for `SelectedNodes` and `SelectedTableCount`, and `NotifyCanExecuteChanged` of the command below). `SelectedNode` keeps its current meaning (the primary node, drives the connection list); setting the selection does not change it.
  - `public IReadOnlyList<ExplorerNode> SelectedTables` (selected nodes with `Kind == ExplorerNodeKind.Table` and `Table is not null`, in tree order) and `public int SelectedTableCount`.
  - `public event EventHandler<TablesRequest>? DropTablesRequested;` with `public sealed record TablesRequest(ConnectionSession Session, IReadOnlyList<TableInfo> Tables)` next to `TableRequest` (XML doc). `WorkspaceViewModel` wires it like `DropTableRequested` (set `ActiveSession = r.Session`, then `DropTablesCommand`); add that line in `WorkspaceViewModel` here.
  - `[RelayCommand(CanExecute = nameof(CanDropSelectedTables))] DropSelectedTables()`: can execute when `SelectedTables.Count >= 1`, **every selected node is a table** (a selected view, folder, connection or database disables it) and all have the same non-null `Session`; it raises `DropTablesRequested`. Hidden tables are not in the tree and so cannot be selected.
  - `Drop(ExplorerNode? node)` (the single command of the context menu): when `node` is in `SelectedTables` and `SelectedTableCount > 1` it behaves as `DropSelectedTables`; otherwise as today (one `DropTableRequested`). Add `public string DropMenuText(ExplorerNode? node)` returning `UiText.Get("explorer.dropTable")` or `UiText.FormatPlural("explorer.dropTables", n)` so both heads build the same menu text; `public bool CanDropMenu(ExplorerNode? node)` false when the selection (containing `node`) mixes kinds or sessions.
- Catalog keys (fr, en, parity test): `explorer.dropTables.one|.other` (`Supprimer {0} tables` / `Drop {0} tables`).
- Selection rules are tested here so the heads only forward what the control selected: a selection that includes a table and a view disables the command, and so does two tables of two connections.

## Out of scope
- The controls (P20-07, P20-08), the drop itself (P20-05).

## Tasks
- [x] Tests first, `tests/Squill.Presentation.Tests/Explorer/ExplorerMultiSelectionTests.cs` (fixtures of `ExplorerTreeViewModelTests.cs`): two tables of one session -> `DropSelectedTablesCommand.CanExecute` true and execute raises one `DropTablesRequested` with both tables in tree order; table + view -> false; tables of two sessions -> false; empty -> false; `DropCommand` on a node inside a 3-table selection raises `DropTablesRequested` (3 tables), on a node outside the selection raises `DropTableRequested` (1); `DropMenuText` singular / plural fr and en; `SetSelectedNodes` with a table removed from the tree after `Reload` does not keep the stale node (filter by presence in `Roots`). Run, see them fail.
- [x] Implement; add the workspace wiring and a workspace test: `DropTablesRequested` ends in `DropTablesCommand` (use the fakes of `WorkspaceDropTablesTests`).

## Acceptance criteria
- Recipe 2 common acceptance; `ExplorerTreeViewModelTests` unchanged and green.

## Risks
- `SelectedNode` and `SelectedNodes` can disagree for a moment while a head updates both: derive everything from `SelectedNodes` and never read `SelectedNode` in the new code.

## Suggested commit message
Select several tables in the explorer view-model and ask the workspace to drop them

## Done notes
- `SelectedNodes`, `SelectedTables`, `SetSelectedNodes`, `DropSelectedTablesCommand`, `DropMenuText`, `CanDropMenu`, `DropTablesRequested` and the workspace wiring added; 10 tests in `ExplorerMultiSelectionTests` plus one in `WorkspaceDropTablesTests`.
- `SelectedTables` is computed in `SetSelectedNodes`; heads must call it again after a reload.
- Full run green (4216 passed, 330 skipped), no flaky test seen.
