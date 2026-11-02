# Visual comparison tooling

PowerShell 7 scripts to run SQuilL on French sample data, capture the window at the mockup size and compare it with a mockup from `docs/design/mockups/`. **Windows only** (Win32 and `System.Drawing` through `Add-Type`).

| File | Purpose |
|------|---------|
| `seed.sql` | SQLite schema (`clients`, `commandes`, `lignes_commande`, `factures`, `produits`, `stocks`, a view, two indexes) and about 60 rows of French data with NULLs |
| `New-VisualProfile.ps1 -Dir <dir>` | creates `<dir>/ventes.db` and `<dir>/profiles.json` (profile `pg-prod-eu`, a SQLite file). Uses the `sqlite3` CLI when it is on PATH, otherwise `Seed.cs` (`dotnet run Seed.cs`, Microsoft.Data.Sqlite pinned as in `Directory.Packages.props`); it prints which one it used |
| `Capture-Window.ps1 -Process <name> -Out <png> [-Width 1440 -Height 900]` | resizes the client area to Width x Height logical px (DPI aware), captures it; exits 1 if the window is missing or minimized |
| `Compare-Mockup.ps1 -Mockup <png> -Capture <png> -Out <png>` | writes mockup \| capture \| absolute difference, each 1440 px wide |

## Run

```powershell
$d = Join-Path $env:TEMP 'squill-visual'
pwsh tools/visual/New-VisualProfile.ps1 -Dir $d
$env:SQUILL_DATA_DIR = $d
rtk bypass dotnet run --project src/Squill.App.Avalonia -c Release   # Avalonia head (process name: Squill.App.Avalonia)
rtk bypass dotnet run --project src/Squill.App.WinUI -c Release      # WinUI head, or the published exe (process name: Squill.App.WinUI)
```

Open the screen by hand (double-click `pg-prod-eu`, open a table or a query), then in another shell:

```powershell
pwsh tools/visual/Capture-Window.ps1 -Process Squill.App.Avalonia -Out $d/m03.png
pwsh tools/visual/Compare-Mockup.ps1 -Mockup docs/design/mockups/M03-Workspace-clair-Windows.png -Capture $d/m03.png -Out $d/m03-compare.png
```

## Theme

Write `<dir>/settings.json` before starting the app (`theme` is `System`, `Light` or `Dark`):

```json
[ { "theme": "Dark", "editorFontSize": 13, "maxGridRows": 1000000, "formatterKeywordCase": "Upper" } ]
```

## Stop

Close the app window, or `Stop-Process -Name Squill.App.Avalonia` / `Squill.App.WinUI`, then `dotnet build-server shutdown`. Remove `$d` and unset `SQUILL_DATA_DIR` when done.
