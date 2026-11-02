# P9-17 - Avalonia head: connection manager window

Status: todo
Depends on: P9-04, P9-05, P9-02

## Goal
The connection editor window matches M02: folder list on the left, engine and environment pickers, form, SSH card, test result bar.

## Design decisions (final)
- Read `docs/design/visual-spec.md` sections 3 and 7 (final). The window keeps the existing view-models (`ConnectionListViewModel`, `ConnectionEditorViewModel`) and gains the `Environment`/`Folder` fields of P9-04.
- Left pane: grouped list with folders (group header collapsible in the view only), `EngineBadge`, environment square; search box filters by name if the list view-model has a filter (else hidden); `+` = new connection, folder button omitted (no folder editing UI beyond the `Dossier` field).
- Engine tiles: the 4 `DatabaseDialect` values; environment tiles: Production/Staging/Développement/Local; `Dossier` is a free-text `ComboBox` (editable) listing existing folders.
- Tabs `Général` and `SSH` only; SSH tab and the right-hand `Tunnel SSH` card show the existing SSH fields.
- Footer: `Tester` (existing test command; result bar `Connexion réussie` + server version/duration if available, error text in `danger` otherwise), `Annuler`, `Enregistrer et se connecter` (save then connect when the editor view-model supports it, else `Enregistrer`; say which in the report).

## Out of scope
- Tags, SSL, proxy, driver tabs, generated URL, extra checkboxes (no data).

## Tasks
- [ ] Rework `ConnectionEditorWindow.axaml`.
- [ ] Headless tests: engine tile selection sets the dialect, environment tile sets `Environment`, grouped list order, save/test buttons enabled states.

## Acceptance criteria
- `rtk bypass dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).
- `rtk dotnet publish src/Squill.App.Avalonia -c Release -r win-x64` succeeds with zero IL2xxx/IL3xxx warnings.
- Visual check (`docs/design/visual-spec.md` section 11): the running app was captured in light and dark with `tools/visual/Capture-Window.ps1`, compared with `M02-Connexions-clair-Windows.png`, `M02-Connexions-sombre-Windows.png` through `tools/visual/Compare-Mockup.ps1`, and every visible difference was fixed or listed in the card report with its reason. If a state could not be reached or driven, the report says which and what the owner must check by hand.
- No process left running (app, `dotnet build-server`, AOT executable).

## Suggested commit message
Avalonia: connection manager window
