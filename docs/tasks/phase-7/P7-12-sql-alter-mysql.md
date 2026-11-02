# P7-12 - Sql: ALTER TABLE for MySQL

Status: done
Depends on: P7-10, P7-07

## Goal
Replace the MySQL `Alter` stub.

## Design decisions (final)
- `MySqlDdlGenerator.Alter(original, edited)` = `TableDiff.Compute(original, edited)` mapped operation by operation. `T` is the qualified, quoted current table name (`original`'s). MySQL cannot change one aspect of a column, so column changes need the full new definition, taken from the matching column of `edited` (found by `Name` equal to `AlterColumn.After.Name`).
- `RenameTable` -> `RENAME TABLE T TO <new>` (keep the schema qualifier on both sides when the table has one).
- `AddColumn` -> `ALTER TABLE T ADD COLUMN <column clause>` (clause as in `CreateTable`, with `AUTO_INCREMENT`).
- `DropColumn` -> `ALTER TABLE T DROP COLUMN <c>`; `RenameColumn` -> `ALTER TABLE T RENAME COLUMN <a> TO <b>` (MySQL 8 / MariaDB 10.5+).
- `AlterColumn` -> **one** statement `ALTER TABLE T MODIFY COLUMN <full column clause of After>` whatever the flags (a default dropped to null means no `DEFAULT` in the clause).
- `DropPrimaryKey` -> `ALTER TABLE T DROP PRIMARY KEY` (the name is ignored); `AddPrimaryKey(cols)` -> `ALTER TABLE T ADD PRIMARY KEY (<cols>)`.
- `CreateIndex` -> `CREATE [UNIQUE] INDEX <n> ON T (<cols>)`; `DropIndex(n)` -> `DROP INDEX <n> ON T`.
- `AddForeignKey` -> `ALTER TABLE T ADD CONSTRAINT <n> FOREIGN KEY (...) REFERENCES ... ON DELETE ... ON UPDATE ...`; `DropForeignKey(n)` -> `ALTER TABLE T DROP FOREIGN KEY <n>`.
- Known limit, to state in a code comment on the class: MySQL commits DDL implicitly, so a failure halfway leaves earlier statements applied (`UsesTransactionalDdl` is false; P7-21 tells the user).

## Out of scope
- Other dialects.

## Tasks
- [x] Implement `Alter` for MySQL and remove its stub.
- [x] Tests in `tests/Squill.Sql.Tests` with exact strings: one per operation, `MODIFY COLUMN` keeps `AUTO_INCREMENT`/default/nullability of the edited column, mixed edit order.
- [x] Docker-gated test (as in `tests/Squill.Data.MySql.Tests`): create, edit (rename column, change type, drop primary key, add index and foreign key), `Alter`, read back and compare.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add MySQL ALTER TABLE generation
