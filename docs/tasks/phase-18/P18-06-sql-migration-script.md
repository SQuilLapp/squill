# P18-06 - Sql: migration script generation

Status: done
Depends on: P18-05

## Goal
Turn a `SchemaComparison` and the user's selection into the SQL script of M09 (`SCRIPT DE MIGRATION`), for the four engines, using the DDL generators that exist. Recipe 1, 4.

## Design decisions (final)
- `src/Squill.Sql/Migration/MigrationScriptBuilder.cs`: `Build(DatabaseDialect dialect, SchemaSnapshot source, SchemaSnapshot target, SchemaComparison comparison, IReadOnlySet<ObjectKey> included, MigrationOptions options)` returns `MigrationScript(string Text, int ChangeCount, IReadOnlyList<MigrationNote> Notes)`.
- Per difference, through `DdlGenerators` / `IDdlGenerator`: `Added` table -> `CreateTable`; `Modified` table -> `Alter(targetDesign, sourceDesign)` (convert `TableInfo` to `TableDesign` with the converter the designer view-model uses; do not write a second one); `Removed` table -> `DropTable`. Views: `CREATE OR REPLACE VIEW` (PostgreSQL, MySQL), `CREATE OR ALTER VIEW` (SQL Server), `DROP VIEW` + `CREATE VIEW` (SQLite); materialized views PostgreSQL `DROP` + `CREATE`. Routines: the engine's `Definition` text for create-or-replace (PostgreSQL `CREATE OR REPLACE`, SQL Server `CREATE OR ALTER`, MySQL `DROP` + `CREATE`), a `DefinitionUnknown` routine is emitted as a comment `-- definition not readable: <name>`. Standalone `Index` differences use `CREATE INDEX` / `DROP INDEX` per dialect when the table itself is not being created or altered in the same script (an index of a created table is already part of `CreateTable`; do not emit it twice).
- Dependency order: drops of views and routines first, then table drops of excluded-by-default objects only when included, then creates / alters of tables (referenced tables before referencing ones, foreign keys of new tables after all creates when there is a cycle), then views and routines, then standalone indexes.
- Defaults match the mockup: an `Added` or `Modified` object is included by default, a `Removed` object (drop on target) is **not** included by default and shows in the script as `-- DROP FUNCTION calc(numeric);  (exclu)` when excluded; the builder takes the explicit `included` set, the defaults are applied by the view-model (P18-08).
- Wrapper: first line `-- <source> -> <target> . genere le <local date time>` (the connection names and the clock come from `MigrationOptions`, never read here), then `BEGIN;` ... `COMMIT;` for PostgreSQL, SQLite, SQL Server (`BEGIN TRANSACTION` / `COMMIT`); MySQL and Oracle: no wrapper and a note `MigrationNote(MySqlImplicitCommit)` (DDL commits implicitly, same fact as phase 15) and a leading comment. Option `WrapInTransaction` (default true) turns the wrapper off. Oracle: not supported, `NotSupportedException` with a clear message (the command is disabled for Oracle by P18-08).
- Each statement ends with `;` on its own line group, comments start with `--`. Identifiers quoted by `SqlQuoting`. Output is deterministic (tests compare text).
- Notes (message codes, `UiText` in P18-08): `DropsData` (a removed table or column), `NarrowsColumn` (type change that can lose data), `MySqlImplicitCommit`, `SqliteRebuild` (an alter that rebuilds the table).

## Out of scope
- Executing the script (P18-08), data compare, cross-dialect scripts.

## Tasks
- [x] Builder, options, notes, ordering.
- [x] Tests in `tests/Squill.Sql.Tests/Migration/`: golden text per dialect for add table + add column + changed column + added index + view change + dropped routine (excluded and included), ordering with a foreign key between two new tables and with a cycle, the wrapper per dialect, `WrapInTransaction=false`, notes, determinism.
- [x] Engine proof: SQLite always (apply the script produced from two real databases, compare again, expect no difference); PostgreSQL, MySQL, SQL Server Docker-gated with the shared sample schema of `tests/Shared/SampleSchema` (two copies, one altered).

## Acceptance criteria
- Common acceptance (recipe 7); applying a generated script then comparing again gives no difference for the included objects on every engine the environment can run (state which ones ran).

## Suggested commit message
Add the migration script builder
