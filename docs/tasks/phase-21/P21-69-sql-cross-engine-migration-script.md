# P21-69 - Sql: PostgreSQL migration script from a SQLite comparison

Status: todo
Depends on: P21-68

## Goal
Point 5 of the index analysis for the pair: the script that makes the PostgreSQL target match the SQLite source (tables, columns, PK, FK, indexes).

Exists today: `MigrationScriptBuilder` (same dialect, from `TableDesign`s), `PostgresDdlGenerator`, `TableDropPlan` ordering.

## Design decisions (final)
- `CrossEngineScriptBuilder.Build(comparison, accepted mappings) -> MigrationScript` (PostgreSQL text): `CREATE TABLE` for `OnlyInSource` tables in FK order, `ALTER TABLE ... ADD COLUMN` / `ALTER COLUMN ... TYPE ... USING` for different columns, PK/FK/index adds and drops; nothing for `EquivalentUnderMapping`.
- A mapping with a `Loss` not accepted by the user is left out with a comment line explaining why.
- `OnlyInTarget` items are **not dropped** unless the option `Drop extra objects` is on (default off).
- Through `TableDesign` + `PostgresDdlGenerator` where possible (no second DDL writer).

## Out of scope
- Running it (P21-70 uses the runner), data copy.

## Tasks
- [ ] Golden tests; Docker-gated round trip: run the script on an empty PostgreSQL database, compare again -> no `Different` / `OnlyInSource` left.

## Acceptance criteria
- Recipe 2 common acceptance.

## Risks
- `ALTER COLUMN TYPE` on existing data may fail: the script is previewed and runs in a transaction (PostgreSQL DDL is transactional).

## Open points (defaults)
- None.

## Suggested commit message
Build the PostgreSQL migration script of a SQLite to PostgreSQL comparison
