# P9-07 - Avalonia head: title bar, menu, activity rail and status bar

Status: todo
Depends on: P9-04, P9-05, P9-02

## Goal
The frame of the main window matches M03: title bar with menu and search, activity rail, status bar.

## Design decisions (final)
- Read `docs/design/visual-spec.md` sections 2, 3 and 4 (final). Strings from the catalog (P9-03) only.

## Out of scope
- Explorer panel, tab strip, editor, results, properties panel (P9-08, P9-11, P9-13).
- Command palette.

## Tasks
- [ ] Replace the toolbar `StackPanel` of `MainWindow.axaml` by the title bar of section 3: app icon, menu (`Fichier`, `Édition`, `Affichage`, `Requête`, `Base de données`, `Outils`, `Aide`) as `Menu` items bound to existing commands (Run/Run all/Cancel/Format/New tab/Close tab/ER diagram/New table/Settings/Open data folder; items without a command stay visible and disabled), the centered search box (non-functional placeholder with `Ctrl+K` chip, disabled, because the command palette does not exist), and the system caption buttons.
- [ ] Activity rail (45 wide): icons for Explorer (active), History, Snippets (`<>` glyph), Diagram, and a settings gear at the bottom; clicking switches the left panel as the current `SidebarView` tabs do today (reuse the existing switching code, do not add features). Icons: reuse `ExplorerGlyphs` or add path geometries to a `Styles/Icons.axaml` (stroke 1.5, 20 px, `currentColor`).
- [ ] Status bar (`Views/StatusBar.axaml`) as section 3: environment block from `ActiveEnvironment` (P9-04; absent when `None`), `<user>@<connection> / <database>` in mono, `StatusText`/`Elapsed` segments (for a finished query `{rows} lignes · {ms} ms` in `success`), right group `Ln, Col`, `UTF-8`, server version. The data-folder button moves into the `Fichier` menu.
- [ ] Window default size 1440 x 900 content, minimum 960 x 600.
- [ ] Headless tests: menu command bindings, status bar environment block hidden for `None` and shown with the right brush per environment, rail selection switches the left panel.

## Acceptance criteria
- `rtk bypass dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).
- `rtk dotnet publish src/Squill.App.Avalonia -c Release -r win-x64` succeeds with zero IL2xxx/IL3xxx warnings.
- Visual check (`docs/design/visual-spec.md` section 11): the running app was captured in light and dark with `tools/visual/Capture-Window.ps1`, compared with `M03-Workspace-clair-Windows.png` and `M03-Workspace-sombre-Windows.png` (frame only) through `tools/visual/Compare-Mockup.ps1`, and every visible difference was fixed or listed in the card report with its reason. If a state could not be reached or driven, the report says which and what the owner must check by hand.
- No process left running (app, `dotnet build-server`, AOT executable).

## Suggested commit message
Avalonia: title bar, menu, activity rail and status bar
