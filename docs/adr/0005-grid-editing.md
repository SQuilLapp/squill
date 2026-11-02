# ADR 0005 - Editable data grid

- Status: accepted
- Date: 2026-11-02

## Context
Phase 8 lets users edit query results in place (change cells, add and delete rows) and write the changes back, on both heads and four dialects, without an ORM and without reflection (Native AOT).

## Decision
- **What is editable.** A result is editable only if its SQL is a single plain `SELECT` over one table whose primary key columns are all selected. This is decided by parsing the SQL (`EditableSourceAnalyzer`), not by guessing from the result. Otherwise the result is read-only, with one of these reasons: not a single statement; not a plain `SELECT`; aggregated or uses `WITH` / `DISTINCT`; does not read a single table; source is not a table (view); table has no primary key; duplicate column names; a primary key column is not selected.
- **Overlay.** Pending changes live in an overlay (`EditableGridDataSource`) over the result. The streamed result is never mutated, and edits are accepted only after the load has finished.
- **Abstract edits, one generator.** The overlay is turned into an abstract `RowEdit` list (`ChangeSetBuilder`). One hand-written generator (`DmlGenerator`) translates it to parameterized SQL with `@pN` placeholders for all dialects. Values are never inlined into SQL. No ORM, no reflection.
- **Keyed on the primary key only.** `UPDATE` and `DELETE` match the primary key and nothing else, so a concurrent change of other columns is overwritten (last write wins). A row that vanished is detected through the affected-row count and rolls the commit back.
- **Execution.** Deletes, then updates, then inserts, in one transaction. MySQL / MariaDB engines that are not transactional are the exception: earlier statements stay applied.
- **Generated keys** are read back per dialect (`RETURNING`, `OUTPUT INSERTED`, `LAST_INSERT_ID()`). Only one generated key column is supported.
- **Typed values.** Text typed by the user is converted by `CellValueParser` according to the CLR type of the column. Other types are passed as text.
- **Shared controller.** `GridEditController` holds the editing behavior so both heads behave identically.
- **Provider type limits.** The DML round-trip tests (P8-13) were meant to find CLR types a provider does not accept as parameters. They have only run on SQLite; the Docker-gated ones (PostgreSQL, MySQL / MariaDB, SQL Server) have never been run, so no limit is known for those servers.

## Not covered
Joins, views, tables without a primary key, read-back of generated columns other than the key, concurrency tokens, bulk edit, an undo stack, export of pending changes.

## Consequences
- A new dialect needs generator and key read-back cases and tests; the overlay, builder and controller do not change.
- Last write wins: two users editing different columns of one row can overwrite each other.
- Not verified: the Docker-gated DML round-trip tests (P8-13) have never been run, and the edit flow was not driven by hand in either head.
- Reopening condition: users need one of the uncovered cases, or the primary-key-only match causes lost updates in practice.
