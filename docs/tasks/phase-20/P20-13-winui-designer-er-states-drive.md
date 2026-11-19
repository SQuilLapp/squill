# P20-13 - WinUI: drive the table designer (M07), the ER diagram (M08) and the shared control states

Status: todo
Depends on: P20-01

## Goal
`visual-spec.md` section 12 records for WinUI: designer (M07) "dark only; row tags, draft state, issue badges not exercised", ER diagram (M08) "capture narrower than 1440 px, `Exporter` item and hops tooltip not checked", shared control styles "popups and pointer-over / pressed not driven". P19-13 did not reach them. Procedure and rules: recipe 3 of [verification-recipe.md](verification-recipe.md).

## Design decisions (final)
- Head: WinUI AOT exe, SQLite `-Scenario Full`, light and dark, French (English only where a label differs).
- M07: open `clients` in the designer (`Concevoir la table`), add a column, rename one, then create issues (duplicate column name, missing type): read the row tags, the draft state and the issue badges; `Appliquer` on `local-sqlite` (restore the file afterwards by re-running the scenario); compare with `M07-Table-*` light and dark. `Commentaire`, impact and the Contraintes / Déclencheurs / DDL tabs are recorded as not built: do not report them again.
- M08: open the ER diagram of `local-sqlite`; make sure the window is exactly 1440 x 900 (`Capture-Window.ps1`); `Exporter` (the item list; SVG if offered; PNG / PDF / Mermaid are recorded as not built); hover a relation with `Drive-Window.ps1 Hover -X -Y` for the hops tooltip; compare with `M08-ER-*`.
- Shared states: pointer-over, pressed and focus on a button, a menu popup and a combo (hover with `Hover`; pressed needs the mouse held: `Click -Mode Mouse` cannot hold, so pressed is `not driven (cannot hold the button)` unless the tool gained a `-Hold` option in P20-01); the popup shadow is recorded, not changed.
- Small fixes with tests where a pattern exists (`tests/Squill.App.WinUI.Tests/TableDesignerFormatTests.cs`, `ErDiagramGeometryTests.cs`, `ControlStylesTests.cs`); bigger ones proposed.

## Out of scope
- Avalonia (P20-19), building the features recorded as not built.

## Tasks
- [ ] Drive, capture, read PNGs; fixes; update the WinUI cells of `visual-spec.md` section 12 (rows M07, M08, shared styles) with the card id.
- [ ] Close everything.

## Acceptance criteria
- Recipe 2 common acceptance if code changed; report lists compared / not driven per area.

## Risks
- The designer apply on the scenario file changes it: use a scratch `-Dir` per pass.
- The ER canvas is custom drawn (not in the UIA tree): use coordinates read from a capture, and say so.

## Suggested commit message
Drive the WinUI designer, ER diagram and control states against their mockups
