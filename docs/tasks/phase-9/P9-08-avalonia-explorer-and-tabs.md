# P9-08 - Avalonia head: object explorer, tab strip and properties panel

Status: done
Depends on: P9-07, P9-02

## Goal
The left panel, the tab strip and the right properties panel match M03.

## Design decisions (final)
- Read `docs/design/visual-spec.md` sections 3 and 4 (final). Strings from the catalog.

## Out of scope
- Query toolbar and editor (P9-11), results (P9-13).

## Tasks
- [x] `SidebarView`/`ObjectExplorerView`: header `EXPLORATEUR` with icon buttons (new connection, refresh, collapse all: bound to existing commands, hidden if none exists), filter box bound to the explorer filter (if the view-model has no filter, keep the box hidden and say so), connection rows grouped by folder (P9-04 `Groups`) with `EngineBadge`, environment `Badge`, connected dot; tree rows 24 high with chevrons, kind icons, right-aligned counts (`ExplorerNode` counts that exist; none otherwise).
- [x] Replace the four `ListBox` strips of `MainWindow.axaml` by one tab strip control (a `TabStrip` or an `ItemsControl` over a merged `Tabs` view-model list; if the view-models are separate lists, build the merged presentation list in the head, not in Presentation) with the tab style of section 3: environment square and top border from the tab's connection environment, unsaved dot where the tab view-model has a dirty flag, close button, `+` button (`NewTabCommand`).
- [x] Right properties panel (264 wide, shown when an explorer node of kind table/view is selected, otherwise hidden): header and `Colonnes`/`Index` lists as section 4 from the schema snapshot the explorer already holds; `Ouvrir l'éditeur de table →` runs the existing design-table command. No row count, size, owner, vacuum lines.
- [x] Headless tests: grouping, tab merge order and close, panel visibility, columns list for a fake schema.

## Acceptance criteria
- `rtk bypass dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).
- `rtk dotnet publish src/Squill.App.Avalonia -c Release -r win-x64` succeeds with zero IL2xxx/IL3xxx warnings.
- Visual check (`docs/design/visual-spec.md` section 11): the running app was captured in light and dark with `tools/visual/Capture-Window.ps1`, compared with `M03-Workspace-*-Windows.png` (explorer, tabs, properties panel) through `tools/visual/Compare-Mockup.ps1`, and every visible difference was fixed or listed in the card report with its reason. If a state could not be reached or driven, the report says which and what the owner must check by hand.
- No process left running (app, `dotnet build-server`, AOT executable).

## Suggested commit message
Avalonia: object explorer, tab strip and properties panel
