# P21-38 - WinUI and Avalonia: Git status marks, diff tab and commit dialog

Status: todo
Depends on: P21-37, P21-35, P21-36

## Goal
Show the P21-37 Git features in both heads.

Exists today: the side-by-side DDL diff of the schema compare view (both heads, P18-09, P18-10).

## Design decisions (final)
- Status letter at the right of each Files panel row (token colours: modified accent, added success, untracked muted).
- Diff tab: reuse the compare view's side-by-side diff control if it can be extracted without changing the compare view's behaviour; otherwise a unified diff in a read-only mono text view (record which).
- Commit dialog: check list, message box, `Commit` / `Cancel`; `Enter` in the message adds a line, `Ctrl+Enter` commits.
- One card for both heads.

## Out of scope
- Anything beyond P21-37.

## Tasks
- [ ] Head tests; drive both exes (recipe 3) on a temp repository.
- [ ] README line, `CHANGELOG.md` line.

## Acceptance criteria
- Recipe 2 and 3 acceptance for both heads.

## Risks
- Extracting the diff control may touch the compare views: re-run their tests.

## Open points (defaults)
- None.

## Suggested commit message
Show Git status, diff and commit in both heads
