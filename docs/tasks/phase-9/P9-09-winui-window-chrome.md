# P9-09 - WinUI head: title bar, menu, activity rail and status bar

Status: done
Depends on: P9-04, P9-06, P9-02

## Goal
The WinUI frame matches M03: title bar with menu and search, activity rail, status bar.

## Design decisions (final)
- Read `docs/design/visual-spec.md` sections 2, 3 and 4 (final). Strings from the catalog (P9-03).

## Out of scope
- Explorer, tab strip, editor, results, properties panel (P9-10, P9-12, P9-15).

## Tasks
- [x] `MainWindow.xaml`: `ExtendsContentIntoTitleBar` with a title bar grid of section 3: app icon, `MenuBar` (`Fichier`, `Édition`, `Affichage`, `Requête`, `Base de données`, `Outils`, `Aide`) bound to the existing handlers (items without a handler stay visible and disabled), the centered search box (disabled placeholder with `Ctrl+K` chip, no command palette exists), `SetTitleBar` for dragging, system caption buttons kept (set the caption button colors from tokens with `AppWindow.TitleBar`).
- [x] Activity rail (45 wide): Explorer (active), History, Snippets, Diagram and the settings gear at the bottom; switching reuses the existing `SidebarView` code. Icons: `PathIcon` geometries in `Styles/Icons.xaml` (stroke 1.5, 20 px).
- [x] `Views/StatusBar.cs` as section 3: environment block from `ActiveEnvironment` (P9-04; absent when `None`), `<user>@<connection> / <database>` mono, `StatusText`/`Elapsed`, right group `Ln, Col`, `UTF-8`, server version; data-folder button moves into the `Fichier` menu.
- [x] Window default size 1440 x 900 content (`AppWindow.Resize` with the DPI scale), minimum 960 x 600.
- [x] Tests in `tests/Squill.App.WinUI.Tests` for the testable parts (status segment formatting, environment brush selection, menu item enabled state).

## Acceptance criteria
- `rtk bypass dotnet build Squill.Windows.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.Windows.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).
- `rtk dotnet publish src/Squill.App.WinUI -c Release -r win-x64` succeeds with zero IL2xxx/IL3xxx warnings.
- Visual check (`docs/design/visual-spec.md` section 11): the running app was captured in light and dark with `tools/visual/Capture-Window.ps1`, compared with `M03-Workspace-clair-Windows.png` and `M03-Workspace-sombre-Windows.png` (frame only) through `tools/visual/Compare-Mockup.ps1`, and every visible difference was fixed or listed in the card report with its reason. If a state could not be reached or driven, the report says which and what the owner must check by hand.
- No process left running (app, `dotnet build-server`, AOT executable).

## Report
- Done: extended title bar (icon, `MenuBar` built in `MainWindow.Chrome.cs`, disabled `Ctrl+K` search placeholder), caption button colors from tokens, activity rail with accent bar, status bar rewritten (environment block, connection, run status, `Ln, Col`, `UTF-8`, server version), data folder moved to the File menu, default size 1440 x 900 client with 960 x 600 minimum. Old toolbar removed as on Avalonia (terminal entry moved to the Tools menu). Tokens are read per element theme (`Views/Tokens.cs`) because code-built brushes must follow the theme override.
- Tests: `ChromeFormatTests` (connection label, environment block, position, enabled state).
- Visual check: AOT exe captured unconnected in light and dark and compared with M03 (frame only): title bar, menu, search, rail, caption buttons and status bar match; menu items are about 8 px tighter than the mockup (the template ignores `Padding`, negative margins used).
- Not compared: connected state (environment block, warning icon, server version, `Ln, Col`, run status) and menu popups, because the connection could not be driven without UI automation; the owner must check them by hand. Explorer, tab strip, editor and results are P9-10..P9-15.
- AOT publish: no IL2xxx/IL3xxx from SQuilL code (only the existing third-party SqlClient/ConfigurationManager trim warnings).

## Suggested commit message
WinUI: title bar, menu, activity rail and status bar
