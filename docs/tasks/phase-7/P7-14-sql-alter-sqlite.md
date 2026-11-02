# P7-14 - Sql: ALTER TABLE for SQLite with table rebuild

Status: todo
Depends on: P7-10, P7-07

## Goal
Replace the SQLite `Alter` stub. SQLite supports only a few `ALTER TABLE` forms; everything else is a table rebuild.

## Design decisions (final)
- `SqliteDdlGenerator.Alter(original, edited)`: compute `ops = TableDiff.Compute(original, edited)`; empty => empty list. If **every** operation is *native* use the native statements, otherwise emit the rebuild for the whole edit (never mix).
- Native operations: `RenameTable` -> `ALTER TABLE "old" RENAME TO "new"`; `RenameColumn` -> `ALTER TABLE T RENAME COLUMN "a" TO "b"`; `CreateIndex` / `DropIndex` -> `CREATE [UNIQUE] INDEX ...` / `DROP INDEX "n"`; `AddColumn` -> `ALTER TABLE T ADD COLUMN <column clause>` only when the column is nullable or has a default, is not auto-increment and is not in the edited primary key; `DropColumn` -> `ALTER TABLE T DROP COLUMN "c"` only when the column is not in the original primary key, not in any original index and not in any original foreign key. Everything else (`AlterColumn`, `AddPrimaryKey`, `DropPrimaryKey`, `AddForeignKey`, `DropForeignKey`, a non-native `AddColumn`/`DropColumn`) forces the rebuild.
- Rebuild statements, in order, for table `T` with temp name `"<name>__squill_new"` (schema `main` omitted): `PRAGMA foreign_keys = OFF`; `CreateTable(edited)` with the new name substituted (its `CREATE TABLE`, no indexes); `INSERT INTO <temp> (<kept cols, new names>) SELECT <same cols, original names> FROM T` where kept columns are those matched by `OriginalName` (new columns take their default, so they are not listed); `DROP TABLE T`; `ALTER TABLE <temp> RENAME TO "<edited name>"`; one `CREATE [UNIQUE] INDEX` per index of `edited`; `PRAGMA foreign_key_check`; `PRAGMA foreign_keys = ON`. Statements 1 and the last two are `PRAGMA`s: P7-21 runs leading and trailing `PRAGMA` statements outside the transaction and everything between inside it, so state that in the XML doc of `Alter`.
- When a column's type changes in a rebuild, the `INSERT ... SELECT` copies the value unchanged (SQLite type affinity converts); no `CAST`.

## Out of scope
- Dependent views and triggers on the table (not recreated: state it in a code comment, and the designer shows a warning in P7-21), other dialects.

## Tasks
- [ ] Implement `Alter` for SQLite and remove its stub.
- [ ] Tests in `tests/Squill.Sql.Tests` with exact strings for native paths and for the rebuild (type change, primary-key change, foreign key added, dropped indexed column), and that a mixed native + non-native edit rebuilds once.
- [ ] Execution tests in `tests/Squill.Data.Sqlite.Tests` on `:memory:` (no Docker): seed rows, run each scenario's statements through the provider, read the schema back and compare with the edited design, check the rows survived (including renamed and retyped columns) and that `PRAGMA foreign_key_check` returns no row.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add SQLite ALTER TABLE generation with table rebuild
