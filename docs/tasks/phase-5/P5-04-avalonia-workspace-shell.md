# P5-04 - Avalonia head: workspace shell

Status: done
Depends on: P5-03

## Goal
The main window (mockup M03 layout): sidebar, query tabs, editor/results split, status bar.

## Design decisions (final)
- If the owner's mockup exists in `docs/design/mockups/` (e.g. `M03*.png`), follow it; otherwise use this layout: left sidebar (width 260, resizable) with `ConnectionList` on top and the object explorer below; main area with a tab strip of `QueryTabViewModel`s, an editor on top and results below separated by a `GridSplitter`; status bar with connection name, server version, `StatusText`, `Elapsed`. All colors, radii, heights from token resources, no hard-coded values.
- Views are `UserControl`s with compiled bindings (`x:DataType`) over the P4 view-models: `SidebarView`, `ObjectExplorerView` (`TreeView` with `TreeDataTemplate`s per `ExplorerNodeKind`), `QueryTabView` (editor host + results host), `StatusBar`. The editor host creates the `AvaloniaSqlEditor` through the workspace's `EditorFactory` and binds it to the tab.
- Toolbar buttons: Run (F5/Ctrl+Enter), Run all, Cancel (Esc while running), New tab (Ctrl+T), Close tab (Ctrl+W), Format (Ctrl+Shift+F), wired to the view-model commands. Theme from `SettingsViewModel.Theme` applied to `RequestedThemeVariant`.
- Results area in this card shows the grid (`DataGridView`) for `Grid` results and a text block for `Message` results; the tab strip for multiple results comes in P5-06.

## Out of scope
- Connection dialog (P5-05), result selection/copy/export (P5-06), history/settings panels (P5-07).

## Tasks
- [x] Implement the views, bindings and shortcuts.
- [x] Run the app against a SQLite file profile created by hand-editing `profiles.json` in a temp data dir (use an env var `SQUILL_DATA_DIR` read by the head to build `AppPaths`), run `select 1`, take a screenshot into the report, close the app.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).
- `rtk dotnet publish src/Squill.App.Avalonia -c Release -r win-x64` succeeds with zero IL2xxx/IL3xxx warnings from Squill code (the package-level IL2104/IL3053 of Microsoft.Data.SqlClient, see `docs/poc/aot-drivers.md`, are tolerated through `WarningsNotAsErrors` in the head project).
- A screenshot or textual description of the running shell with a query result is included in the card report.
- No process left running.

## Suggested commit message
Add Avalonia workspace shell

## Report
- Ran the published AOT exe with `SQUILL_DATA_DIR` on a SQLite profile: connected, status bar showed `Local SQLite 3.53.4` and `Completed 1 statement(s)`. No screenshot of the result grid; the headless test `WorkspaceShellTests` checks that the grid receives the result.
- Deviation: one `TreeDataTemplate` with a kind-to-glyph converter instead of one template per `ExplorerNodeKind`. `AppServices` gained `Settings` (theme).
