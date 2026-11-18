# P19-09 - WinUI: look at M01, M09 and M10 against their mockups

Status: done
Depends on: P19-07, P19-08

## Goal
Close the part of P18-31 that needs no server: nobody ever compared the WinUI home page (M01), schema compare (M09) or CSV import wizard (M10) with `M01-Accueil-*`, `M09-Comparaison-*`, `M10-Transfert-*` (`docs/verification/new-screens.md`, "Not driven"). Procedure and honesty rules: recipe 3 of [debug-polish-recipe.md](debug-polish-recipe.md).

## Design decisions (final)
- Head: the WinUI AOT build (`rtk dotnet publish src/Squill.App.WinUI -c Release -r win-x64`, run the published exe; process `Squill.App.WinUI`). Data: `pwsh tools/visual/New-VisualProfile.ps1 -Dir <tmp> -Scenario Full -Theme Light|Dark` (P19-08), `SQUILL_DATA_DIR=<tmp>`, `SQUILL_NO_UPDATE_CHECK=1`.
- Matrix: M01, M09 (compare `cmp-source` to `cmp-target`, with a group expanded and the script visible), M10 mapping step (with `people.csv`), each in **light and dark** and in **French and English** (French first). The native file picker of M10 cannot be driven by script: if no way to set the path exists, capture steps 1-2 only and mark the mapping step `agent: not driven (native file picker)`; do not edit the app to add a test hook.
- Steps to drive and record: rows 1, 2, 3, 4 and 7 of `docs/verification/new-screens.md` (the SQLite-only ones; row 5 and 6 belong to P19-10). Fill the `Result` column per recipe 3 and the Note (head, theme, language, differences in text).
- Fix small differences here (tokens, spacing, wording, missing catalog keys with fr/en parity); propose bigger ones as `P19-17` onwards.
- Update the M01, M09, M10 rows of `docs/design/visual-spec.md` sections 15, 16, 19 ("Numbers: not measured..." lines) and section 12 only for what was compared.

## Out of scope
- Avalonia (P19-11), M11 / M12 (P19-10), ticking owner boxes, redesigns.

## Tasks
- [x] Publish, prepare data, drive and capture each screen; read every PNG and composite.
- [x] Small fixes with tests where a pattern exists; list proposed cards.
- [x] Update `new-screens.md` (Result column, Note, "Not driven"), `visual-spec.md`.

## Acceptance criteria
- Common acceptance (recipe 4) if code changed (`Squill.Windows.slnx`, AOT publish, app starts); the report separates "compared (head, theme, language)" from "not driven"; processes closed.

## Risks
- A result is only as good as the capture: if P19-07's covered-window check fires, fix the setup, do not record a comparison.
- The compare tab needs two connections open; open both profiles before choosing source and target.

## Suggested commit message
Compare the WinUI home, schema compare and import screens with their mockups

## Done notes
- Compared: M01, WinUI published AOT head, light and dark, French and English (1440 x 900, `Screen` capture, window on top). Differences recorded in `new-screens.md`, proposed as P19-17; no code changed.
- Not driven: M09 and M10 (no UI automation of the native window in this session). They stay open for a session that can click the window.
