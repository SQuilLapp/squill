# Visual comparison tooling

PowerShell 7 scripts to run SQuilL on French sample data, capture the window at the mockup size and compare it with a mockup from `docs/design/mockups/`. **Windows only** (Win32 and `System.Drawing` through `Add-Type`).

| File | Purpose |
|------|---------|
| `seed.sql` | SQLite schema (`clients`, `commandes`, `lignes_commande`, `factures`, `produits`, `stocks`, a view, two indexes) and about 60 rows of French data with NULLs |
| `New-VisualProfile.ps1 -Dir <dir> [-Scenario Basic\|Full] [-Theme Light\|Dark]` | creates `<dir>/ventes.db` and `<dir>/profiles.json` (profile `pg-prod-eu`, a SQLite file). Uses the `sqlite3` CLI when it is on PATH, otherwise `Seed.cs` (`dotnet run Seed.cs`, Microsoft.Data.Sqlite pinned as in `Directory.Packages.props`); it prints which one it used. `-Scenario Full` adds the data of `docs/verification/new-screens.md`, see "Scenario Full" |
| `scenario.sql` | extra SQL of `-Scenario Full`, one block per database (`-- @db <name>` lines) |
| `Capture-Window.ps1 -Process <name> -Out <png> [-Width 1440 -Height 900] [-Method Screen\|PrintWindow]` | resizes the client area to Width x Height logical px (DPI aware), captures it; see "Capture-Window" below |
| `Compare-Mockup.ps1 -Mockup <png> -Capture <png> -Out <png>` | writes mockup \| capture \| absolute difference, each 1440 px wide |

## Run

```powershell
$d = Join-Path $env:TEMP 'squill-visual'
pwsh tools/visual/New-VisualProfile.ps1 -Dir $d
$env:SQUILL_DATA_DIR = $d
rtk bypass dotnet run --project src/Squill.App.Avalonia -c Release   # Avalonia head (process name: Squill.App.Avalonia)
rtk bypass dotnet run --project src/Squill.App.WinUI -c Release      # WinUI head, or the published exe (process name: Squill.App.WinUI)
```

`SQUILL_LANGUAGE` (`fr`, `en` or `system`) overrides the saved language for a run, without saving it; set it before launching to capture the English or French screens.

`SQUILL_NO_UPDATE_CHECK=1` skips the start-up update flow (scheduled check and the one-time question) in both heads; set it for captures and test runs.

Open the screen by hand (double-click `pg-prod-eu`, open a table or a query), then in another shell:

```powershell
pwsh tools/visual/Capture-Window.ps1 -Process Squill.App.Avalonia -Out $d/m03.png
pwsh tools/visual/Compare-Mockup.ps1 -Mockup docs/design/mockups/M03-Workspace-clair-Windows.png -Capture $d/m03.png -Out $d/m03-compare.png
```

## Scenario Full

`pwsh tools/visual/New-VisualProfile.ps1 -Dir <dir> -Scenario Full` (re-runnable: it recreates only its own files, ids are deterministic) writes in `<dir>`:

- six SQLite profiles `pg-prod-eu` (Production), `mysql-staging` (Staging), `sqlserver-dev` (Development), `local-sqlite` (Local), `pg-staging-us` (Staging), `sqlserver-prod` (Production), each `<name>.db` built from `seed.sql`, and `recent.json` listing them newest first (5 min, 90 min, 26 h, 3 days, 9 days, 40 days ago, relative to the run): M01;
- `cmp_source.db` / `cmp_target.db` and profiles `cmp-source` (Staging) / `cmp-target` (Production): table `archive_2023` only in the source, table `avoirs` only in the target, column `produits.poids_kg` and index `ix_factures_numero` only in the target, view `v_ca_par_client` different: M09;
- `people.csv` (UTF-8, `;`, header, 40 rows with accents and one quoted field containing `;`; rows 12, 25 and 33 fail: text in the integer key, `2024-02-31`, empty required name) and `import_target.db` with an empty `people` table (`id`, `nom` required, `naissance`, `montant`), profile `import-target`: M10;
- `settings.json` only with `-Theme Light|Dark` (an older one is removed otherwise).

It prints a table of file, profile, environment and purpose. `-Scenario Basic` (default) is the single `pg-prod-eu` profile.

## Capture-Window

- `Screen` (default): makes the window topmost for the capture (it flashes to the front for about a second and may take focus from your own work), checks that the window at the center of the client area belongs to the target process, copies the screen, then drops the topmost flag. The success line says `window on top: yes`.
- `PrintWindow`: `PrintWindow` with `PW_RENDERFULLCONTENT`; works while the window is covered. A DirectComposition / WinUI 3 window may come back black: that is detected and reported (exit 3). Known limit: on the WinUI head `PrintWindow` returned SQuilL but shrunk and cropped (about 2/3 size, white margins, no title bar), so use it only to see that a covered window is alive; `Screen` is the supported method for comparisons. The Avalonia head renders correctly with both.
- Exit codes: 0 saved; 1 window missing or minimized; 2 the window is covered by another window (`Window of '<process>' is covered by '<other>'`, no PNG written, for example an always-on-top screen recorder); 3 `PrintWindow` returned a black bitmap.

## Theme

Write `<dir>/settings.json` before starting the app (`theme` is `System`, `Light` or `Dark`):

```json
[ { "theme": "Dark", "editorFontSize": 13, "maxGridRows": 1000000, "formatterKeywordCase": "Upper" } ]
```

## Stop

Close the app window, or `Stop-Process -Name Squill.App.Avalonia` / `Squill.App.WinUI`, then `dotnet build-server shutdown`. Remove `$d` and unset `SQUILL_DATA_DIR` when done.

## Drive-Window

`pwsh tools/visual/Drive-Window.ps1 -Action <action> -Process <name> [...]` drives the window of a running head with UI Automation, the mouse and the keyboard (Windows only, PowerShell 7, no package). `-Process` is `Squill.App.Avalonia` or `Squill.App.WinUI`; the target is the main window, or the dialog chosen by `Focus-Dialog` while it stays open.

| Action | Use |
|--------|-----|
| `Dump [-Depth n] [-Filter text]` | prints the UI Automation tree (`ControlType`, `Name`, `Id`, `Class`, `[x,y,w,h]` screen px) |
| `Click -Name text [-ControlType Button] [-Index n] [-Mode Invoke\|Mouse\|DoubleClick\|RightClick]` | exact `Name` first, then contains; `Invoke` (default) uses the Invoke / SelectionItem / ExpandCollapse / Toggle pattern and falls back to a real mouse click; the other modes always use the mouse. `-X -Y` (client logical px) replaces `-Name` for canvases |
| `Key -Keys "Ctrl+K Esc"` | chords separated by spaces, sent with `SendInput`; keys: modifiers `Ctrl Shift Alt Win`, `Enter Esc Tab Space Up Down Left Right Home End PageUp PageDown Delete Backspace F1-F12`, letters, digits |
| `Type -Text text` | `SendInput` with `KEYEVENTF_UNICODE` (accents, `;`, quotes) |
| `Wait -Name text [-TimeoutSeconds 10] [-Gone]` | polls until the element exists (or is gone); a name that also exists elsewhere (the title-bar search box has the palette input's name) never goes |
| `Hover -Name text` or `-X -Y` | moves the pointer without clicking |
| `Focus-Dialog` | brings a secondary window of the process to the front and makes it the target of the next actions (its tree is not in the main window tree); it is remembered in `$env:TEMP/squill-drive-target.txt` until that window closes |
| `SelfTest` | opens a throwaway WinForms window, types into it, asserts the text, closes it: prints `selftest ok` |

Exit codes: 0 ok, 1 window missing, 2 cannot get focus, 4 element missing (the nearest names are printed), 5 timeout, 6 bad arguments or self-test failure.

**Foreground rule**: `Key`, `Type`, `Hover` and mouse clicks first check that the foreground window belongs to the target process and try to bring it to the front (`AttachThreadInput` + `SetForegroundWindow`); when that fails the script exits 2 and sends nothing, so keys never reach another program. `Invoke` clicks do not need the focus. Use `Dump` (add `-Filter`) to find names; empty names are listed below.

### Names to click (Dump of both heads, SQLite `-Scenario Full`, French)

| Need | Avalonia | WinUI |
|------|----------|-------|
| Open a connection (home) | `Click -Name pg-prod-eu -ControlType Button` (Invoke) | same (Invoke) |
| Menus | `Fichier`, `Édition`, `Affichage`, `Requête`, `Base de données`, `Outils`, `Aide` (MenuItem) | same (MenuBarItem) |
| Editor | `Code editor` (Edit, `Click -Mode Mouse`, then `Key Ctrl+A`, `Type`) | same |
| Run / explain | `Exécuter` (Id `RunButton`), `Expliquer`, `Analyser`, `Arrêter`, `Script`; or `Key Ctrl+Enter` | `Expliquer`, `Arrêter`, `Auto-commit`, `Manuel` named; `Exécuter`, `Script`, `Analyser` have an empty name: use `Key Ctrl+Enter` / `Ctrl+Maj+Entrée` or `Dump` coordinates |
| Palette | `Key Ctrl+K`, then the Edit named `Rechercher objets, fichiers, actions...` (second one in the tree); `Key Esc` | same |
| Explorer filter | Edit Id `FilterBox` | Edit `Filtrer les objets` |
| Explorer rows | `TreeViewItem`, empty names: use `Dump` coordinates and `Click -X -Y`/`-Mode Mouse` | `ListViewItem` named `Squill.Presentation.Explorer.ExplorerNode`: same |
| Left rail, sidebar buttons, tab header, format/history/export | Ids `RailExplorer`, `RailHistory`, `RailSnippets`, `RailDiagram`, `RailSettings`, `NewConnectionButton`, `RefreshButton`, `CollapseButton`, `TabStrip`, `FormatButton`, `HistoryButton`, `ExportButton` (empty names; `Dump` shows Ids, the script clicks by `Name` only: use their coordinates) | empty names and mostly empty Ids (Ids `Rail*`, `NewConnectionButton`, `RefreshButton`, `CollapseButton`): coordinates |
| Result search | Edit `Rechercher` | Edit `Rechercher` |

Driven on both heads: open `pg-prod-eu` (Invoke on the home card), `Click -Name "Code editor" -Mode Mouse`, `Key Ctrl+A`, `Type "select * from clients"`, `Key Ctrl+Enter` (12 rows), `Key Ctrl+K` (palette open, input present), `Key Esc`. Only the editor click needed `Mouse`; `Invoke` worked for the cards. Gap: about 14 controls per head have empty accessibility names (rail 5, sidebar 3, explorer rows, tab header, WinUI `Exécuter`/`Script`/`Analyser`, format/history/export): more than the 12 the card allows, so no head was changed; adding `AutomationProperties.Name` (also needed by the Narrator checklist rows) is left to a card.
