# P18-31 - Verification: the five new screens against their mockups

Status: todo
Depends on: P18-04, P18-10, P18-15, P18-21, P18-27

## Goal
One pass over M01, M09, M10, M11 and M12 in both heads, and the manual checklist for what an agent cannot drive. Same honesty rule as P17-17: no visual match is claimed that was not looked at.

## Design decisions (final)
- Automated part: an end-to-end scenario per feature already exists in the Presentation cards (compare, security, import) or the Data cards (activity); this card adds what is missing across them and **runs the Docker-gated ones together** (`rtk pwsh eng/run-docker-tests.ps1 -Engine <engine>` for postgres, mysql, sqlserver) and reports pass / skipped per engine.
- Captures (`visual-spec.md` section 11): M01 (with a populated recent list: open a few SQLite profiles first), M09 (two SQLite files with differences), M10 mapping step (a real CSV into a SQLite table), M12 and M11 against PostgreSQL containers kept with `-Keep` (open transaction + blocked update; roles from P18-17), in both themes and both heads. Compare with the PNGs and list differences.
- Write `docs/verification/new-screens.md` in the format of `docs/verification/grid-filter-palette.md`: Automated (done) with what each scenario covers, Not driven, Known limits (the recipe 1 table), How to prepare, one checklist per screen with the WinUI / Avalonia boxes left for the owner, a Note.
- Small visible differences are fixed here; bigger ones are proposed as cards `P18-33` onwards.

## Tasks
- [ ] Run the Docker-gated suites, captures, comparison, fixes.
- [ ] `docs/verification/new-screens.md`; update `visual-spec.md` sections 10 and 12 and the table of `docs/design/mockups/README.md` ("In the app today").

## Acceptance criteria
- Common acceptance (recipe 7); every container and process closed (`podman ps -a` empty); report separates "automated, ran", "captured and compared", "not driven".

## Suggested commit message
Verify the home, compare, import, security and sessions screens
