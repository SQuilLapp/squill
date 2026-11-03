# P9-02 - Tools: sample data and window capture for visual comparison

Status: done
Depends on: none

## Goal
Give every later card the same way to run the app on realistic French data, capture it at the mockup size and compare it with a mockup.

## Design decisions (final)
- Create `tools/visual/` (PowerShell 7 scripts and a SQL file, no C# project):
- `seed.sql`: a SQLite database script creating tables `clients`, `commandes`, `factures`, `lignes_commande`, `produits`, `stocks` with the columns drawn in M03, M05, M07, M08 (for example `commandes(id, client_id, statut, montant_ttc, cree_le, note)`), primary and foreign keys, indexes `ix_commandes_client_id` and `ix_commandes_cree_le`, a view, and at least 40 rows of French sample data (company names of M03/M05 such as `Atelier Lumen SARL`, cities `Lyon`, `Paris`, `Marseille`, some NULLs).
- `New-VisualProfile.ps1 -Dir <temp dir>`: creates the SQLite file from `seed.sql` (through the `sqlite3` CLI when it is on PATH; otherwise through the file-based app `tools/visual/Seed.cs` run with `rtk bypass dotnet run tools/visual/Seed.cs`, declaring `#:package Microsoft.Data.Sqlite@<version of Directory.Packages.props>`; document which one was used in `tools/visual/README.md`), and writes a profile store file in the format of `Squill.Platform` (`rtk grep -n "class JsonProfileStore" src`) into the directory so that setting `SQUILL_DATA_DIR=<dir>` starts the app with a connected-ready profile named `pg-prod-eu` (display name chosen so that the sidebar looks like the mockups; the database is SQLite).
- `Capture-Window.ps1 -Process <name> -Out <png> [-Width 1440 -Height 900]`: finds the main window of the process, sets its client area to Width x Height logical px (scaled by the monitor DPI, using `SetWindowPos` through `Add-Type`), brings it to front, captures the client area through `Graphics.CopyFromScreen`, and prints the saved size. It exits non-zero with a clear message when the window is not found or is minimized.
- `Compare-Mockup.ps1 -Mockup <png> -Capture <png> -Out <png>`: scales both images to the same width (1440) and writes a side-by-side composite (mockup left, capture right) plus a third column that is the absolute difference, so a Read of the output shows both and the delta.
- `tools/visual/README.md`: the exact commands to run the Avalonia head (`rtk bypass dotnet run --project src/Squill.App.Avalonia -c Release`) and the WinUI head (`rtk bypass dotnet run --project src/Squill.App.WinUI -c Release`, or the published exe) with `SQUILL_DATA_DIR`, how to switch theme (via the app settings file of the data dir), and the stop procedure.
- Scripts use only PowerShell 7 and .NET `System.Drawing.Common`/Win32 through `Add-Type`; they run on Windows only and say so.

## Out of scope
- Any change under `src/`.
- UI automation (clicking); screens that need interaction are reached by hand or reported.

## Tasks
- [x] Write the four files and the README; run the whole chain once on the Avalonia head (start with seed data, capture, compare with `M03-Workspace-clair-Windows.png`) and attach the composite path in the card report.

## Acceptance criteria
- The chain above ran on this machine and produced a composite image that was read; the report lists the exact commands that worked.
- No change outside `tools/visual/`.
- No process left running (app, `dotnet build-server`, AOT executable).

## Suggested commit message
Add visual comparison tooling and French sample data
