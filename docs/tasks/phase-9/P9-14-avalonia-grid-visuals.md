# P9-14 - Avalonia head: grid canvas drawing

Status: done
Depends on: P9-13, P9-02

## Goal
The grid canvas draws like M05: fonts, header with types, row states, number column, selection, NULL, density.

## Design decisions (final)
- Read `docs/design/visual-spec.md` sections 3 and 6 (final).
- `DataGridCanvas` stays a custom-drawn control; only brushes, typefaces, geometry change. Row height from `SquillRowHeight*` (density), mono typeface `SquillFontMono` at the `mono-grid` size for cells, UI typeface for header names.
- Header: name 13 semibold then the type name in mono 11 `fg-3` (type text from the column metadata that exists, e.g. `int8`), sort arrow in `accent` when the grid has a sort (draw only if a sort state exists), primary-key diamond for key columns (from `ReadOnlyColumns`/editor key info if available, else omit).
- Row-number column 36 wide right-aligned `fg-3`; states: added (`+`, 3 px `success` bar, `cell-added` tint), deleted (`-`, 3 px `danger` bar, strike, `cell-deleted` tint), modified cell tint `cell-modified`; in-place editor border 2 px `accent` (P8-14 editor keeps working); selected row/cells `selection`.
- Numbers right-aligned, text left; NULL italic `cell-null`; placeholders `(auto)`/`DEFAULT` italic `fg-3` in added rows if the edit controller exposes them.

## Out of scope
- Column resizing, column menu, filters, the value panel (no feature).

## Tasks
- [x] Update the drawing code and the existing grid tests (headless render test: a 3-row grid with each state renders without exception and pixel samples at the state-bar positions equal the expected token colors).

## Acceptance criteria
- `rtk bypass dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).
- `rtk dotnet publish src/Squill.App.Avalonia -c Release -r win-x64` succeeds with zero IL2xxx/IL3xxx warnings.
- Visual check (`docs/design/visual-spec.md` section 11): the running app was captured in light and dark with `tools/visual/Capture-Window.ps1`, compared with `M05-Grille-clair-Windows.png`, `M05-Grille-sombre-Windows.png` (grid area only), `M03-Workspace-*-Windows.png` through `tools/visual/Compare-Mockup.ps1`, and every visible difference was fixed or listed in the card report with its reason. If a state could not be reached or driven, the report says which and what the owner must check by hand.
- No process left running (app, `dotnet build-server`, AOT executable).

## Suggested commit message
Avalonia: grid canvas drawing
