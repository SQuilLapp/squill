# P12-14 - End-to-end verification of query variables

Status: todo
Depends on: P12-06, P12-10, P12-12, P12-13

## Goal
Drive the whole feature the way the user does, on a real database, in the WinUI head first and the Avalonia head as a smoke test, and write the report.

## Design decisions (final)
- Report `docs/verification/variables.md` in the format of `docs/tasks/phase-11/verification-recipe.md` section 2 (`Feature | WinUI | Avalonia | Result | Note`).
- Scenario (SQLite in both heads, PostgreSQL in WinUI through the container script): create a global `schema` variable and an environment `limit`, type `SELECT * FROM {{ident:table}} WHERE id = {{id}} LIMIT {{raw:limit}}`; see `table` and `id` unresolved (colour, hover text, chip), create `id` from the quick action in the Connection scope, create `table` in Global, run, see rows; change the connection value and re-run; make `id` secret and check it is masked everywhere and still works; set a Tab value; try `' OR '1'='1` as `id`; rename and delete; delete the connection and check its variables are gone; switch language fr/en; light/dark.
- Bugs: fix in the same commit when under about 30 lines with a regression test, otherwise a new card `P12-16` onwards (status todo, added to the index).
- Never mark a step verified that was not driven; list it under "to check by hand" with exact clicks.

## Out of scope
- New features.

## Tasks
- [ ] Run the scenario, write the report, fix or card the bugs.

## Acceptance criteria
- Report committed; builds and tests of both slnx green; no container, app or build server left running.

## Suggested commit message
Verify query variables end to end
