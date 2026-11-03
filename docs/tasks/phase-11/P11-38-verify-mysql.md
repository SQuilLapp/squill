# P11-38 - End-to-end verification: MySQL

Status: todo
Depends on: P11-37, P11-18, P11-24, P11-27, P11-28, P11-30, P11-31, P11-32

## Goal
Same as P11-37 against a MySQL 8.4 container.

## Design decisions (final)
- Start the container with `-Keep` (P11-37). Feature list: that of P11-36 plus MySQL specifics: database = schema (the unified tree shows one schema level, check it reads sensibly), switching database (P11-24), routines and triggers and their definitions (`DELIMITER`-free wrapping), `CALL` with two result sets, `AUTO_INCREMENT` read-back after insert, `tinyint(1)` as boolean in grid and editor, `enum`/`set`, zero dates, `json`, backtick quoting in completion and generated DML, `LIMIT` in paging, implicit commit message in the designer, TLS require, wrong password message, `SLEEP` cancel, terminal `mysql` client when installed, `ON DUPLICATE KEY UPDATE` run, case sensitivity of table names (lower_case_table_names) in the explorer.
- Output: `docs/verification/mysql.md`, fixes with regression tests, new cards for large bugs.

## Out of scope
- MariaDB, features absent from the app.

## Tasks
- [ ] Drive, fix, report.

## Acceptance criteria
- Report complete. `rtk dotnet build Squill.Windows.slnx -c Release -warnaserror` 0 warnings; `rtk bypass dotnet test --solution Squill.Windows.slnx -c Release` green; AOT publishes clean; no container or process left.
- To check by hand (owner): the report's list, max 10 steps.

## Suggested commit message
Verify MySQL end to end and record the report
