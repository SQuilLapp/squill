# P11-30 - Table designer DDL on PostgreSQL, MySQL and SQL Server

Status: done
Depends on: P11-02, P11-03, P11-04

## Goal
The same operation matrix as P11-29 runs against the three servers; the existing `*AlterTests` cover a few operations only and never ran before P11-02 ... P11-04.

## Design decisions (final)
- A shared scenario list `DesignerScenarios` in `tests/Shared` (a source file linked into the four test projects like `DmlScenario`; reuse it from P11-29 so the matrix is written once) and one Docker-gated class `DesignerMatrixTests` per server project.
- Server specifics to cover on top of the P11-29 list: PostgreSQL type change needing `USING` (text to integer) and `serial` vs identity, rename of an index, column with a default referencing a sequence, schema other than `public`; MySQL `AUTO_INCREMENT` add/remove, `CHANGE`/`MODIFY` keeping nullability and default, index prefix on `text`, FK drop by name, implicit commit (a failure mid-script leaves earlier DDL applied: the designer must say so in its error text; check the message and test it with a failing second statement); SQL Server dropping a column that has a default constraint or an index (generator must drop the constraint first), `sp_rename` for column and table, identity add (requires rebuild: the generator must refuse with an issue, not emit invalid SQL), schema other than `dbo`, `nvarchar(max)`.
- Each failure is a generator bug (fix with a unit test in `Squill.Sql.Tests`) or a documented refusal (the designer's validator reports it before apply; add the validator rule with message codes in both catalogs).

## Out of scope
- UI.

## Tasks
- [x] Shared scenarios, three test classes, fixes.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` 0 warnings; default test run green; `rtk pwsh eng/run-docker-tests.ps1 -Engine all` green; no container left.

## Suggested commit message
Run the designer operation matrix on the three server engines
