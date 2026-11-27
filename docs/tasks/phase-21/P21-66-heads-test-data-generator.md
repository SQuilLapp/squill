# P21-66 - WinUI and Avalonia: the test data generator dialog

Status: todo
Depends on: P21-65

## Goal
Reach `TestDataViewModel` from the explorer table menu (`Generate test data...`) in both heads.

Exists today: the import wizard windows (P18-26, P18-27) and their progress/summary pages.

## Design decisions (final)
- One window: row count, a column grid (name, type, generator combo, null ratio), a 10-row preview, `Generate`; then the import wizard's progress and summary pages reused.
- One card for both heads.

## Out of scope
- None.

## Tasks
- [ ] Head tests; drive both exes (recipe 3) on the SQLite scenario (1,000 rows into `commandes`).
- [ ] README line, `CHANGELOG.md` line.

## Acceptance criteria
- Recipe 2 and 3 acceptance for both heads.

## Risks
- None specific.

## Open points (defaults)
- None.

## Suggested commit message
Add the test data generator dialog to both heads
