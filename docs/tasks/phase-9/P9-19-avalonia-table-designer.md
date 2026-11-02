# P9-19 - Avalonia head: table designer view

Status: todo
Depends on: P9-08, P9-02

## Goal
The table designer matches M07.

## Design decisions (final)
- Read `docs/design/visual-spec.md` sections 3 and 8 (final). Strings from the catalog.
- Layout: header (icon, name, environment pill, subtitle, draft state, `Annuler les modifications`, `Appliquer` + chip), sub-tabs `Colonnes`/`Index`/`Clés étrangères` with counts (existing designer sections), toolbar buttons, the columns table as a `DataGrid` alternative: keep the existing editing controls (text boxes, combos, checkboxes) but styled as section 8 (row 30, header 28, mono names/types, `syn-type` color for the type text), row state tags `MODIFIÉE`/`NOUVELLE` from the row view-model state, `Colonne sélectionnée` card for the selected column, right `APERÇU DDL` panel with the generated script highlighted by the same tokenizer/highlighter as the editor (`SqlColorizer`).
- Issues from `DesignValidator` keep their current display (restyle with `Badge` danger/warning), placed under the table.

## Out of scope
- Impact card, Contraintes/Déclencheurs/DDL tabs, rollback script option (no feature), new designer functionality.

## Tasks
- [ ] Rework `TableDesignerView.axaml`.
- [ ] Headless tests: row tag visible for modified/new rows, DDL panel shows the script, apply disabled when issues exist.

## Acceptance criteria
- `rtk bypass dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).
- `rtk dotnet publish src/Squill.App.Avalonia -c Release -r win-x64` succeeds with zero IL2xxx/IL3xxx warnings.
- Visual check (`docs/design/visual-spec.md` section 11): the running app was captured in light and dark with `tools/visual/Capture-Window.ps1`, compared with `M07-Table-clair-Windows.png`, `M07-Table-sombre-Windows.png` through `tools/visual/Compare-Mockup.ps1`, and every visible difference was fixed or listed in the card report with its reason. If a state could not be reached or driven, the report says which and what the owner must check by hand.
- No process left running (app, `dotnet build-server`, AOT executable).

## Suggested commit message
Avalonia: table designer view
