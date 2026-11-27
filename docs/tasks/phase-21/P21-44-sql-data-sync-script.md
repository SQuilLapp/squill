# P21-44 - Sql: data sync script from the row differences

Status: todo
Depends on: P21-43

## Goal
Turn `RowDifference`s into an `INSERT` / `UPDATE` / `DELETE` script that makes the target table equal to the source, per dialect.

Exists today: `DmlGenerator` (INSERT, UPDATE, DELETE with key `WHERE`, quoting, literals per dialect), `MigrationScript` (P18-06) as the script holder shape.

## Design decisions (final)
- `DataSyncScriptBuilder.Build(dialect, target table, key columns, columns, differences, options)` -> `MigrationScript`-like text: deletes first (children before parents is the caller's concern for multi-table, P21-45 compares one table at a time), then updates (changed columns only), then inserts; literals inlined (the script is meant to be read, saved and run), batched `INSERT ... VALUES (...), (...)` up to 500 rows where the dialect allows it.
- Options: include deletes (default on), identity insert on SQL Server (`SET IDENTITY_INSERT` around inserts when the key is an identity).

## Out of scope
- Running the script (the compare tab uses the statement runner, P21-45).

## Tasks
- [ ] Golden tests per dialect (quoting, NULL, dates, binary, identity insert, batching).

## Acceptance criteria
- Recipe 2 common acceptance.

## Risks
- Inlined literals of large values: cap a single literal at 1 MB and fail the build with a clear message beyond.

## Open points (defaults)
- None.

## Suggested commit message
Build a data sync script from row differences
