# P7-04 - Data.MySql: read auto-increment and foreign-key actions

Status: todo
Depends on: P7-01, P1-12

## Goal
`MySqlSchemaReader` fills the new model members.

## Design decisions (final)
- `IsAutoIncrement` is true when `INFORMATION_SCHEMA.COLUMNS.EXTRA` contains `auto_increment` (case-insensitive).
- Foreign-key actions come from `INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS.DELETE_RULE` / `UPDATE_RULE` (`NO ACTION`, `RESTRICT`, `CASCADE`, `SET NULL`, `SET DEFAULT`), parsed with `ReferentialActions.Parse`.
- `PrimaryKeyName` stays `null` (the name is always `PRIMARY`).
- Extend the existing queries; no extra round trip per table.

## Out of scope
- Other engines.

## Tasks
- [ ] Extend the reader.
- [ ] Extend the Docker-gated tests (as in P1-12): an `AUTO_INCREMENT` column, a column without it, each action.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Read auto-increment and foreign-key actions from MySQL
