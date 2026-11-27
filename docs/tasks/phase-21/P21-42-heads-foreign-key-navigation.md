# P21-42 - WinUI and Avalonia: foreign key navigation from the grid

Status: todo
Depends on: P21-41, P21-40

## Goal
Reach P21-41 from both grids: a context menu entry and a key, plus a small marker on FK header cells.

Exists today: the grid context menu of P21-40; header drawing with sort/filter marks (P17-07, P17-09).

## Design decisions (final)
- Context menu: `Go to referenced row` (`Ctrl+B` in the grid; with several FKs on the column, a submenu per FK) and `Referencing rows >` (one item per child table and FK).
- Header: a small key glyph on FK columns (both canvases, token colour).
- One card for both heads.

## Out of scope
- None.

## Tasks
- [ ] Head tests; drive both exes (recipe 3) on the SQLite scenario (`commandes.client_id` -> `clients`).
- [ ] README line, `CHANGELOG.md` line.

## Acceptance criteria
- Recipe 2 and 3 acceptance for both heads.

## Risks
- `Ctrl+B` must not clash with an existing grid key: check both key maps.

## Open points (defaults)
- None.

## Suggested commit message
Navigate foreign keys from the grid in both heads
