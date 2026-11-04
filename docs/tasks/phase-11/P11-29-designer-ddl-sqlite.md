# P11-29 - Table designer on SQLite: create and alter matrix

Status: done
Depends on: P11-16

## Goal
Every designer operation produces SQL that SQLite accepts and preserves data, including the table rebuild path (P7-14).

## Design decisions (final)
- Data-driven test class `DesignerMatrixTests` in `tests/Squill.Data.Sqlite.Tests` (never skipped). Operations, each on a table with 100 rows, a foreign key pointing into it, an index and a trigger: add column (nullable, with default, `NOT NULL` with default), drop column (plain, indexed, in a FK), rename column, change type, change nullability, change default, add/drop primary key, composite PK, add/drop unique index, add/drop foreign key with each action, rename table, drop table. After each: read the schema back and compare with the target design, the row count and a checksum of a surviving column are unchanged, `PRAGMA foreign_key_check` and `PRAGMA integrity_check` return no row/`ok`, index and trigger still exist (the rebuild must re-create them: fix `SqliteDdlGenerator` if it loses them), and the FK of the other table still resolves.
- Foreign keys enforced during the rebuild: the generated script runs with `PRAGMA foreign_keys=off` inside the transaction rules of SQLite (`PRAGMA foreign_keys` is a no-op inside a transaction: the script must set it before `BEGIN`; verify the runner order, fix if wrong).
- Type affinity: the catalog types the designer offers for SQLite all round trip through the reader to the same catalog entry.

## Out of scope
- Server dialects (P11-30).

## Tasks
- [x] Matrix tests, generator/runner fixes.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` 0 warnings; `rtk bypass dotnet test --solution Squill.slnx -c Release` green.

## Suggested commit message
Test every table designer operation on SQLite and fix rebuild losses
