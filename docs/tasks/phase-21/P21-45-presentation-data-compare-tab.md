# P21-45 - Presentation: the data compare tab

Status: todo
Depends on: P21-43, P21-44

## Goal
A `Data compare` tab: choose a source and a target (connection, database, table; same dialect), compare, browse the differences, generate and run (or save) the sync script.

Exists today: `SchemaCompareViewModel` (source/target pickers over sessions and databases, compare, script, `ExecuteScriptCommand` through the runner and production confirmation, save `.sql`); `RowComparer` and `DataSyncScriptBuilder`.

## Design decisions (final)
- `DataCompareViewModel`: pickers like the schema compare (reuse its picker view-model if it can be shared without changing its behaviour), table pair (same name by default, choosable), key columns (the primary key; refused without one), compared columns (common columns, choosable), `Compare` (runs two ordered `SELECT`s on two sessions, streams into `RowComparer` off the UI thread, cancellable), differences list (kind, key, changed columns with source/target values), summary counts, `Generate script`, `Run script` (statement runner + production confirmation of the target), `Save .sql`.
- Several tables at once is not built (one pair per compare); a list of tables to run one after the other is a later card if wanted.
- Entry points: explorer table menu `Compare data with...`, palette action.

## Out of scope
- Heads (P21-46, P21-47), cross-dialect data compare.

## Tasks
- [ ] Tests on two SQLite files (identical, differences of each kind, no key refused, cancel, production confirmation on run); Docker-gated test on PostgreSQL (two databases).

## Acceptance criteria
- Recipe 2 common acceptance.

## Risks
- Two sessions on the same connection profile: use two connections, never share one reader.

## Open points (defaults)
- None.

## Suggested commit message
Add the data compare tab view-model
