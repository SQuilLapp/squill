# P21-64 - WinUI and Avalonia: column statistics in the column menu

Status: todo
Depends on: P21-63

## Goal
Show P21-63 in the column menu of both grids.

Exists today: `ColumnMenu` in both heads (sort, value filter, P17-07, P17-09).

## Design decisions (final)
- A `Statistics` expander in the column menu: label/value rows (mono numbers), a small bar histogram (drawn in a canvas, token colour), the "over N loaded rows" header and `Compute on server`.
- One card for both heads.

## Out of scope
- None.

## Tasks
- [ ] Head tests; drive both exes (recipe 3) on numeric, date and text columns.
- [ ] README line, `CHANGELOG.md` line.

## Acceptance criteria
- Recipe 2 and 3 acceptance for both heads.

## Risks
- Avalonia popups are invisible to captures: check through the view-model (recipe 3).

## Open points (defaults)
- None.

## Suggested commit message
Show column statistics in the column menu of both heads
