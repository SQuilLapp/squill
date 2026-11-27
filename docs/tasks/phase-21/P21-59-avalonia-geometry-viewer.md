# P21-59 - Avalonia: geometry viewer canvas

Status: todo
Depends on: P21-57, P21-58

## Goal
Same as P21-58 in the Avalonia head, then the README and changelog lines.

Exists today: Avalonia custom-drawn canvases (`DataGridCanvas`, `ErDiagramCanvas`, `PlanGraphCanvas`).

## Design decisions (final)
- `GeometryCanvas` with `StreamGeometry`, same interactions and toggles as P21-58.

## Out of scope
- WinUI.

## Tasks
- [ ] Headless tests; publish and drive (recipe 3).
- [ ] README line, `CHANGELOG.md` line.

## Acceptance criteria
- Recipe 2 and 3 acceptance (Avalonia).

## Risks
- None beyond P21-58.

## Open points (defaults)
- None.

## Suggested commit message
Draw geometries on a canvas in the Avalonia head
