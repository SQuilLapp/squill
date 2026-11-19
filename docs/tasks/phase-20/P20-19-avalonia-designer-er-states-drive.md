# P20-19 - Avalonia: drive the table designer (M07), the ER diagram (M08) and pointer states

Status: todo
Depends on: P20-13

## Goal
P19-15 drove the Avalonia ER diagram in dark French (toolbar, six tables, relations, minimap, `Exporter` -> `SVG vectoriel`) but not the designer (M07), the hops tooltip, the light theme, English, or pointer-over / pressed states. Repeat P20-13 on Avalonia. Procedure and rules: recipe 3 of [verification-recipe.md](verification-recipe.md).

## Design decisions (final)
- Head: Avalonia AOT exe, SQLite `-Scenario Full`, light and dark, French; English only where a label differs; scale 1.5 pixel sizes as in P20-16.
- Read the Done notes of P20-13 first and reuse its findings and fixes. Same areas: M07 (add / rename column, duplicate-name and missing-type issues, row tags, draft state, issue badges, `Appliquer`), M08 (1440 x 900 logical, the `Exporter` item list, hops tooltip by `Hover` over a relation), shared states (pointer-over and focus on a button, a menu and a combo; pressed as in P20-13).
- Compare with `M07-Table-*` and `M08-ER-*`; small fixes with tests (`tests/Squill.App.Avalonia.Tests/DesignerUiTests.cs`, `DiagramUiTests.cs`); propose bigger ones.

## Out of scope
- WinUI, features recorded as not built, Linux / macOS.

## Tasks
- [ ] Drive, capture, read PNGs; fixes; update the Avalonia cells of `visual-spec.md` section 12 (rows M07, M08, shared styles) with the card id.
- [ ] Close everything.

## Acceptance criteria
- Recipe 2 common acceptance if code changed; report lists compared / not driven per area.

## Risks
- Designer apply changes the scenario file: scratch `-Dir` per pass. The ER canvas is custom drawn: coordinates from a capture.

## Suggested commit message
Drive the Avalonia designer, ER diagram and control states against their mockups
