# P9-26 - Explorer: one tree connection > database > schema > objects

Status: todo
Depends on: P9-08, P9-10

## Goal
The explorer is a single tree as in mockup M03: every connection is a root node, and a connected one expands to database > schema > tables, views and columns. The separate connection list above the tree (P9-08, P9-10) disappears in both heads.

## Design decisions (final)
- Read `docs/design/visual-spec.md` sections 3 and 4 (final). Strings from the catalog.
- Presentation owns the tree. Add `ConnectionNode` (or an `ExplorerNodeKind.Connection` / `Database` plus a `Profile` and `IsConnected` on `ExplorerNode`; pick the smaller change) and an explorer root view-model (extend `ObjectExplorerViewModel` or add `ExplorerTreeViewModel` in `Squill.Presentation/Explorer/`) that exposes `Roots`, built from `ConnectionListViewModel.Profiles`, `WorkspaceViewModel.Sessions` and the folder grouping of P9-04. Folder headers stay as `Folder` nodes above the connection nodes (same grouping and ordering rules as today's `ConnectionRow` lists; move that logic to Presentation, delete the two head-side `ConnectionRow.cs`/`WorkspaceTabList`-style duplicates only if nothing else uses them).
- A connection node has children only while connected: one `Database` node named after `ConnectionProfile.Database` (fall back to the profile name when null), then the existing schema > tables/views > columns/indexes/foreign keys nodes built from that session's `SchemaSnapshot` (reuse `ObjectExplorerViewModel.Rebuild`'s node building; one level of nodes per session, not only the active one). A disconnected connection node is a leaf and shows no chevron.
- Selecting any node of a connection makes its session the `ActiveSession` (explorer-driven, replaces `Explorer = new ObjectExplorerViewModel(...)` per session switch), so tabs, status bar and properties panel keep following the selection. The properties panel still shows for a selected table/view node only.
- Kept unchanged: engine badge, environment `Badge` (catalog text `PRODUCTION`/`STAGING`/`DEV`/`LOCAL`), connected dot, folder grouping, context menus (connection nodes: New connection, Edit, Delete, Connect, Disconnect; table nodes: the existing Design/Drop items), double-tap connects a disconnected connection node and opens a table/view node, header buttons (new connection, refresh, collapse all), filter box. The filter matches titles at every level and keeps ancestors of a match visible, as today.
- Refresh refreshes the sessions of the selected node's connection (existing `RefreshSchemaAsync`). No new data is invented: no counts where none exist, no database list beyond the profile's own database.
- Empty state (no profile) keeps the existing "No connections yet" text and button, shown over the tree area.

## Out of scope
- Listing other databases of the server, lazy loading, drag and drop, tab strip and properties panel look (P9-08/P9-10 stay as they are).

## Tasks
- [ ] Presentation: node kinds and properties for connection/database/folder nodes, the unified `Roots` with folder grouping, connect/disconnect/session change updating the tree in place (expand the node that was just connected), filter over all levels, active-session selection. Keep `OpenTableRequested`, `DesignTableRequested`, `DropTableRequested` events wired to `WorkspaceViewModel`.
- [ ] Avalonia: `SidebarView`/`ObjectExplorerView` use one `TreeView` with a template per node kind (connection row: chevron, `EngineBadge`, name, environment `Badge`, connected dot; folder row; database row; existing schema/table/view/column rows), 24 high rows, indent 16; remove the `Profiles` `ListBox`, its separator and the head-side `ConnectionRow` if now unused. Context menus and double-tap as listed above.
- [ ] WinUI: same in `SidebarView.xaml`/`SidebarPanel` with one `TreeView` (`ItemTemplateSelector` or `DataTemplate` per kind, existing 28 high row note of P9-10 still applies, say so in the report); remove the connection list and the duplicate `ConnectionRow`.
- [ ] Tests: Presentation tests for the tree (grouping and order, connected node gets database > schema > tables, disconnected node is a leaf, session change from selection, filter keeps ancestors, connect/disconnect updates in place); headless Avalonia and WinUI tests for the head-side templates/selection that can run without a screenshot; state what needs the screenshot.

## Acceptance criteria
- `rtk bypass dotnet build Squill.slnx -c Release -warnaserror` and `rtk bypass dotnet build Squill.Windows.slnx -c Release -warnaserror` exit 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` and `rtk bypass dotnet test --solution Squill.Windows.slnx -c Release` are green (new tests included, none skipped except Docker-gated ones).
- `rtk dotnet publish src/Squill.App.Avalonia -c Release -r win-x64` and `rtk dotnet publish src/Squill.App.WinUI -c Release -r win-x64` succeed with zero IL2xxx/IL3xxx warnings.
- Visual check (`docs/design/visual-spec.md` section 11): the running app was captured in light and dark with `tools/visual/Capture-Window.ps1`, compared with `M03-Workspace-*-Windows.png` (explorer, tabs, properties panel) through `tools/visual/Compare-Mockup.ps1`, and every visible difference was fixed or listed in the card report with its reason. If a state could not be reached or driven, the report says which and what the owner must check by hand.
- No process left running (app, `dotnet build-server`, AOT executable).

## Suggested commit message
Explorer: single tree from connection to objects
