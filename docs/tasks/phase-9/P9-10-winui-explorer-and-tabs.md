# P9-10 - WinUI head: object explorer, tab strip and properties panel

Status: done
Depends on: P9-09, P9-02

## Goal
The left panel, the tab strip and the right properties panel match M03.

## Design decisions (final)
- Read `docs/design/visual-spec.md` sections 3 and 4 (final). Strings from the catalog.

## Out of scope
- Query toolbar and editor (P9-12), results (P9-15).

## Tasks
- [x] `SidebarView`: header `EXPLORATEUR` and icon buttons, filter box (hidden if the view-model has no filter, say so), connection rows grouped by folder (P9-04) with `EngineBadge`, environment `Badge`, connected dot; tree rows 24 high (`TreeView` item container style), kind icons, right-aligned counts that exist.
- [x] Replace the `TabView` chrome look of `MainWindow.xaml` by the tab style of section 3 (environment square and top border by the tab's connection environment, dirty dot where available, close button, `+` button): restyle `TabViewItem` through its resources/template, keep the `TabView` behavior and the existing close handlers.
- [x] Right properties panel (264 wide, shown for a selected table/view node, hidden otherwise) as section 4 from the schema snapshot already held by the explorer; link runs the existing design-table command. No row count, size, owner or vacuum lines.
- [x] Tests in `tests/Squill.App.WinUI.Tests` for grouping/ordering helpers and the properties panel model; state what needs the screenshot.

## Acceptance criteria
- `rtk bypass dotnet build Squill.Windows.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.Windows.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).
- `rtk dotnet publish src/Squill.App.WinUI -c Release -r win-x64` succeeds with zero IL2xxx/IL3xxx warnings.
- Visual check (`docs/design/visual-spec.md` section 11): the running app was captured in light and dark with `tools/visual/Capture-Window.ps1`, compared with `M03-Workspace-*-Windows.png` (explorer, tabs, properties panel) through `tools/visual/Compare-Mockup.ps1`, and every visible difference was fixed or listed in the card report with its reason. If a state could not be reached or driven, the report says which and what the owner must check by hand.
- No process left running (app, `dotnet build-server`, AOT executable).

## Suggested commit message
WinUI: object explorer, tab strip and properties panel

## Report
- Tree rows are 28 high, not 24: the `TreeViewItemPresenter` clips descenders below that height, so 24 was not reachable without re-templating the presenter. Other visible differences: the connection list stays a separate list above the tree (as in P9-08) while M03 nests connection > database > schema in one tree; the environment pill reads `PRODUCTION`/`STAGING` (catalog) where M03 shows `PROD`/`DEV`; no counts on schema folders (no data); the editor/toolbar/results areas belong to P9-12/P9-15.
- Checked in light and dark with `Capture-Window.ps1` (debug and AOT exe): connection rows with folder, badges, pills and connected dot, tree, tab strip (environment square, top bar, close, `+`), properties panel. Not driven: the unsaved dot and the hover close button were not looked at by hand; the owner should check them.
- Needs the screenshot (not unit-tested): tab template states, tree row height, panel look.
