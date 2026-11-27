# P21-68 - Core: cross-engine comparer ("equivalent under mapping")

Status: todo
Depends on: P21-67

## Goal
Point 4 of the index analysis for the SQLite to PostgreSQL pair: compare a SQLite schema with a PostgreSQL schema through the neutral model and explain each difference.

Exists today: `SchemaComparer` / `SchemaComparison` (same dialect, strict equality), the type mapper of P21-67.

## Design decisions (final)
- `CrossEngineComparer.Compare(source snapshot, target snapshot, mapping options) -> CrossEngineComparison`: tables matched by name (case-insensitive for the pair, as both fold to lower case in practice for SQLite-created names), columns by name; verdict per item `Equal`, `EquivalentUnderMapping` (with the mapping used), `Different` (with the reason), `OnlyInSource`, `OnlyInTarget`; primary keys, foreign keys (columns and referenced table; referential actions compared), indexes (columns, unique; names ignored).
- Normalization (point 3) only for what the pair needs: `INTEGER PRIMARY KEY` vs `bigint GENERATED ... AS IDENTITY` / `serial` = equivalent; defaults compared as normalized text with a warning when not equal.
- Reuses the result shape of `SchemaComparison` where possible so the compare view can show it (P21-70).

## Out of scope
- Views, routines, triggers, check constraints.

## Tasks
- [ ] Tests over two snapshots built in code (every verdict), then a Docker-gated test: the SQLite scenario vs a PostgreSQL database created from it by hand-written DDL.

## Acceptance criteria
- Recipe 2 common acceptance.

## Risks
- Name matching: a SQLite table `Clients` vs PostgreSQL `clients` - match case-insensitively but report the case difference.

## Open points (defaults)
- None.

## Suggested commit message
Compare a SQLite schema with a PostgreSQL schema through the neutral type model
