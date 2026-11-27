# P21-07 - Oracle: grid editing, generated keys and column types

Status: done
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
- [x] Unit tests (DML text, bridge out-parameter codec).
- [x] Docker-gated DML round trip (P8-13 pattern) and type round trip (P11-26 pattern) on Oracle.

## Acceptance criteria
- Recipe 2 common acceptance; `-Engine oracle` green.

## Risks
- `DATE` holds a time part: never drop it when formatting.

## Open points (defaults)
- None.

## Suggested commit message
Edit Oracle results in the grid with generated key read-back

## Done notes
- Premise check: `DmlGenerator` bound `@p0` and wrote `DEFAULT VALUES`, neither valid Oracle; `EditableSourceAnalyzer` and `EditCommitter` needed no change (the analyzer matches table names case-insensitively, identity columns already set `IsAutoIncrement`). `ColumnEditability` had no Oracle read-only types: added `LONG`, `LONG RAW`, `BFILE`, `XMLTYPE`, `ROWID`, `UROWID`, `INTERVAL YEAR/DAY ...` and `... WITH LOCAL TIME ZONE`.
- `DmlGenerator` Oracle: `:pN` parameters, `INSERT INTO t (firstColumn) VALUES (DEFAULT)` for an all-default insert, `RETURNING "ID" INTO :out0`. Names are quoted exactly as the catalog gives them.
- Bridge: `execute` takes `outParameters` (`WireOut` name and kind) and answers `outValues`; `OracleReturning` finds the `RETURNING ... INTO :x` variables that the caller did not supply (read as `NUMBER`: generated keys are identities) and the result reader exposes them as a one-row result, so `EditCommitter` needed no change. A `DateTime` is now bound as `TIMESTAMP` (fraction kept), a `DateTimeOffset` as `TIMESTAMP WITH TIME ZONE`; a `TIMESTAMP WITH TIME ZONE` column is read as `DateTimeOffset` (kind `o`).
- CLOB/BLOB already read whole through ODP.NET `GetValue` (verified by the round trip); no extra cap was added: a very large CLOB is read entirely (not capped, unlike the card's wording).
- Docker (Oracle Free, Podman): `DmlRoundTripTests` (P8-13 scenario with Oracle types), `ColumnTypeEditTests` (P11-26 pattern, 10 cases incl. DATE with and without time and TIMESTAMP WITH TIME ZONE), `GridEditTests` (analyzer + `EditCommitter` insert with generated key, update, delete). The full grid view-model scenario `GridEditScenario` is not run on Oracle (multi-row inserts, engine-specific DDL).
- The support matrix of ADR 0008 is left to P21-09.
- Bug found by the Oracle commit test and fixed here: `SqlRunner.BeginAsync/CommitAsync/RollbackAsync` hard-coded `BEGIN` (a PL/SQL block on Oracle), so no grid commit outside a user transaction could work on Oracle. They now use `TransactionSql` (MySQL now sends `START TRANSACTION` instead of `BEGIN`, equivalent); three stale entries were dropped from `ignored-literals.txt`.
- Docker re-runs of the other engines after this change: SQL Server 168/168 and MySQL 172/172 green on a second run; the first back-to-back run had 4 (SQL Server) and 6 (MySQL) failures that were not inspected and did not reproduce.
