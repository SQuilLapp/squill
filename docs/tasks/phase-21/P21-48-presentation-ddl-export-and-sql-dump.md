# P21-48 - Presentation: export a schema's DDL and dump data as generated SQL

Status: todo
Depends on: P21-44

## Goal
Export the DDL of a schema (or chosen tables) to a `.sql` file, optionally with the data as `INSERT` statements, for every engine, without any external tool.

Exists today: `Script as CREATE TABLE` for one table (explorer), `DdlGenerators.For(dialect).CreateTable` from a `TableDesign`, `TableDropPlan` (foreign key order), view and routine definitions in the snapshot (P11-15 ... P11-19), `DataSyncScriptBuilder` insert batching (P21-44), `ResultFileWriter`.

## Design decisions (final)
- `SchemaDumpWriter` (Presentation): for the chosen tables, in FK order (parents first; a cycle: tables first, then `ALTER TABLE ... ADD CONSTRAINT` for the FKs of the cycle), `CREATE TABLE` (with PK, indexes, FKs), then views and routines from their stored definitions (as text, in name order, with a warning comment that dependencies between views are not sorted), then optionally data (`INSERT` batches streamed from `SELECT *` per table, ordered by PK).
- Streams to the file (never the whole dump in memory), cancellable, progress per table; written UTF-8.
- `DumpOptions`: tables (default all of the schema), include data, include views/routines, `DROP ... IF EXISTS` first (default off), max rows per table (default unlimited).
- A generated dump is **portable within the dialect only**; the file header says engine and version.

## Out of scope
- Engine tools (P21-49), UI (P21-50), cross-dialect dump.

## Tasks
- [ ] Tests on the SQLite scenario: dump then restore into a new file through the script runner gives the same schema (`SchemaComparer` identical) and the same rows (`RowComparer` identical); FK cycle case; cancel leaves a partial file deleted.
- [ ] Docker-gated round trip on PostgreSQL, MySQL, SQL Server.

## Acceptance criteria
- Recipe 2 common acceptance.

## Risks
- Types that do not round-trip as literals (PostgreSQL arrays, spatial): listed in the header as skipped columns rather than written wrong.

## Open points (defaults)
- None.

## Suggested commit message
Export schema DDL and dump table data as generated SQL
