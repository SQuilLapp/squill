# Visual comparison tooling

PowerShell 7 scripts to run SQuilL on French sample data, capture the window at the mockup size and compare it with a mockup from `docs/design/mockups/`. **Windows only** (Win32 and `System.Drawing` through `Add-Type`).

| File | Purpose |
|------|---------|
| `seed.sql` | SQLite schema (`clients`, `commandes`, `lignes_commande`, `factures`, `produits`, `stocks`, a view, two indexes) and about 60 rows of French data with NULLs |
| `New-VisualProfile.ps1 -Dir <dir>` | creates `<dir>/ventes.db` and `<dir>/profiles.json` (profile `pg-prod-eu`, a SQLite file). Uses the `sqlite3` CLI when it is on PATH, otherwise `Seed.cs` (`dotnet run Seed.cs`, Microsoft.Data.Sqlite pinned as in `Directory.Packages.props`); it prints which one it used |
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
