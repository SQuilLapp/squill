# P21-07 - Oracle: grid editing, generated keys and column types

Status: todo
Depends on: P21-03, P21-05

## Goal
Results of a plain `SELECT` over one Oracle table can be edited and committed like on the other engines.

Exists today: `DmlGenerator`, `EditableSourceAnalyzer`, generated key read-back per dialect (P8-04), `docs/column-types.md` for four engines; Oracle has none of it.

## Design decisions (final)
- `DmlGenerator` Oracle branch: `:name` parameters, upper-case quoting rules of P21-05.
- Generated key read-back: `INSERT ... RETURNING id INTO :out` needs an output parameter: add an `outParameters` field to the bridge `execute` op (name and kind) and return their values; no other engine changes.
- Column types: read and write `NUMBER`, `VARCHAR2`, `CLOB` (as text, capped like other long values), `BLOB` (bytes), `DATE` (date-time), `TIMESTAMP WITH TIME ZONE` (`DateTimeOffset`), `RAW` (bytes); others read-only `<unsupported ...>`. Add an Oracle column to `docs/column-types.md`.
- Commit inside the session transaction (savepoints from P21-03).

## Out of scope
- `ROWID`-based editing of tables without a primary key.

## Tasks
- [ ] Unit tests (DML text, bridge out-parameter codec).
- [ ] Docker-gated DML round trip (P8-13 pattern) and type round trip (P11-26 pattern) on Oracle.

## Acceptance criteria
- Recipe 2 common acceptance; `-Engine oracle` green.

## Risks
- `DATE` holds a time part: never drop it when formatting.

## Open points (defaults)
- None.

## Suggested commit message
Edit Oracle results in the grid with generated key read-back
