# P21-40 - WinUI and Avalonia: Copy as submenu in the grid and Script as items in the explorer

Status: todo
Depends on: P21-39

## Goal
Reach the P21-39 formats from the grid's context menu and the explorer's table menu in both heads.

Exists today: grid copy key (`Ctrl+C`) in both `DataGridCanvas`es; explorer context menus with `Script as CREATE TABLE`.

## Design decisions (final)
- Grid context menu (both heads): `Copy` (TSV, unchanged), `Copy as >` with the formats, disabled items with a tooltip reason; `Ctrl+Shift+C` repeats the last chosen format.
- Explorer: `Script as >` submenu replacing the single `Script as CREATE TABLE` item (CREATE TABLE stays first).
- Code-built menus (no `ItemsSource`, WinUI). One card for both heads.

## Out of scope
- None.

## Tasks
- [ ] Head tests; drive both exes (recipe 3), paste results into a query tab and run them on SQLite.
- [ ] README line (editing data section), `CHANGELOG.md` line.

## Acceptance criteria
- Recipe 2 and 3 acceptance for both heads.

## Risks
- The grid has no context menu today in one head: check first; if missing, add a minimal one with `Copy` and `Copy as`.

## Open points (defaults)
- None.

## Suggested commit message
Add Copy as and Script as menus to both heads
