# P21-28 - WinUI and Avalonia: the rename dialog with its preview

Status: todo
Depends on: P21-27

## Goal
Show `RenameRefactoringViewModel` in both heads, reachable from the explorer menu (table and column rows), the designer (column name cell) and the editor (`Shift+F6`).

Exists today: code-built dialogs in both heads (`ProductionConfirmDialog` / `ProductionConfirmWindow` style); explorer context menus.

## Design decisions (final)
- A dialog with the new-name box, the scope check boxes, a preview tree (source -> edits, before/after on two mono lines) and `Rename` / `Cancel`; `Enter` in the name box refreshes the preview, never applies (phase 15 rule: `Enter` never confirms a write).
- One card for both heads (WinUI first in the work order).

## Out of scope
- None.

## Tasks
- [ ] Text-check (WinUI) and headless (Avalonia) tests; drive both published exes (recipe 3) on the SQLite scenario: rename a column used in an open tab and a snippet.
- [ ] README section line, `CHANGELOG.md` line.

## Acceptance criteria
- Recipe 2 and 3 acceptance for both heads.

## Risks
- Long previews: virtualize the list (WinUI `ListView` built in code without `ItemsSource` binding traps: follow P20-27).

## Open points (defaults)
- None.

## Suggested commit message
Show the rename refactoring dialog in both heads
