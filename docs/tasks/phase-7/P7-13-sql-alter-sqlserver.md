# P7-13 - Sql: ALTER TABLE for SQL Server

Status: done
Depends on: P7-10, P7-07

## Goal
Replace the SQL Server `Alter` stub.

## Design decisions (final)
- `SqlServerDdlGenerator.Alter(original, edited)` = `TableDiff.Compute(original, edited)` mapped operation by operation. `T` is the qualified, quoted current table name; `Q(x)` is `SqlQuoting.Literal` of the *unquoted* qualified name, as `sp_rename` needs it.
- `RenameTable` -> `EXEC sp_rename N'<schema>.<old>', N'<new>'` (new name unqualified, unquoted).
- `AddColumn` -> `ALTER TABLE T ADD <column clause>` (with `IDENTITY(1,1)` and explicit `NULL`/`NOT NULL`).
- `DropColumn` -> `ALTER TABLE T DROP COLUMN <c>`, but first, when the column may carry a default constraint, the same statement list begins with the **default drop batch** below (one statement string).
- `RenameColumn` -> `EXEC sp_rename N'<schema>.<table>.<a>', N'<b>', N'COLUMN'`.
- `AlterColumn`: `Type` or `Nullability` -> `ALTER TABLE T ALTER COLUMN <c> <type> [NOT NULL|NULL]` (always states nullability); `Default` -> the **default drop batch** (when the old default was not empty) followed, when the new default is not empty, by `ALTER TABLE T ADD DEFAULT <expr> FOR <c>`. A `Type` change on a column with a default also needs the default dropped first and re-added afterwards: emit drop batch, `ALTER COLUMN`, then `ADD DEFAULT` when the edited column has one.
- **Default drop batch** (SQL Server default constraints are auto-named, so look the name up; one statement string, sent as one batch): `DECLARE @n sysname = (SELECT dc.name FROM sys.default_constraints dc JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id WHERE dc.parent_object_id = OBJECT_ID(N'<schema>.<table>') AND c.name = N'<column>'); IF @n IS NOT NULL EXEC(N'ALTER TABLE T DROP CONSTRAINT ' + QUOTENAME(@n))` (single line).
- `DropPrimaryKey(name)` -> `ALTER TABLE T DROP CONSTRAINT <quoted name>`; the name is always known for SQL Server (P7-05). `AddPrimaryKey` -> `ALTER TABLE T ADD PRIMARY KEY (<cols>)`.
- `CreateIndex` -> as `CreateTable`; `DropIndex(n)` -> `DROP INDEX <n> ON T`.
- `AddForeignKey` / `DropForeignKey` -> `ALTER TABLE T ADD CONSTRAINT ...` / `ALTER TABLE T DROP CONSTRAINT <n>`; `SetDefault` action is valid, `Restrict` is written as `NO ACTION` for this dialect.

## Out of scope
- Other dialects, toggling IDENTITY on an existing column (rejected by P7-08).

## Tasks
- [x] Implement `Alter` for SQL Server and remove its stub.
- [x] Tests in `tests/Squill.Sql.Tests` with exact strings: one per operation, the default batch text, type change on a column with a default (three statements), `Restrict` mapped to `NO ACTION`, mixed order.
- [x] Docker-gated test (as in `tests/Squill.Data.SqlServer.Tests`): create a table with a default, change its type and default, rename a column, add a foreign key, run `Alter`, read back and compare.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add SQL Server ALTER TABLE generation
