# P7-05 - Data.SqlServer: read identity, foreign-key actions and key name

Status: todo
Depends on: P7-01, P1-14

## Goal
`SqlServerSchemaReader` fills the new model members.

## Design decisions (final)
- `IsAutoIncrement` = `sys.columns.is_identity = 1`.
- Foreign-key actions come from `sys.foreign_keys.delete_referential_action` / `update_referential_action`: 0 NoAction, 1 Cascade, 2 SetNull, 3 SetDefault (SQL Server has no Restrict).
- `PrimaryKeyName` = `sys.key_constraints.name` where `type = 'PK'` for the table, `null` when none.
- Extend the existing queries; no extra round trip per table.

## Out of scope
- Other engines, default-constraint names (the SQL Server generator looks them up at run time, P7-13).

## Tasks
- [ ] Extend the reader.
- [ ] Extend the Docker-gated tests (as in P1-14): identity column, each action, a named primary key and an auto-named one (name starts with `PK__`).

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Read identity, foreign-key actions and key name from SQL Server
