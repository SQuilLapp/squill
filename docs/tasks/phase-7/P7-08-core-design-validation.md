# P7-08 - Core: table design validation

Status: todo
Depends on: P7-07

## Goal
Report everything wrong (or risky) in an edited design before any SQL is generated.

## Design decisions (final)
- In `Squill.Core.Design`: `public enum DesignIssueSeverity { Error, Warning }`, `public sealed record DesignIssue(DesignIssueSeverity Severity, string Message, string? Subject)` (`Subject` = column, index or key name, `null` for table-level) and `public static class DesignValidator { public static IReadOnlyList<DesignIssue> Validate(TableDesign edited, TableDesign? original, DatabaseDialect dialect); }` (`original == null` means "new table").
- Errors: empty table name; no columns; empty or duplicate (case-insensitive) column names; empty data type; primary-key column missing from the table or listed twice; a nullable primary-key column; more than one auto-increment column; an auto-increment column that is not the sole primary-key column (SQLite) or not part of the primary key (MySQL); `IsAutoIncrement` toggled on a column that exists in `original`; empty or duplicate index name, empty or duplicate foreign-key name; index or foreign-key column missing from the table, or an empty column list; foreign key with different numbers of local and referenced columns, or an empty referenced table; `Schema` different from the original's (moving a table between schemas is not supported); for SQLite a non-null `Schema` other than `main`.
- Warnings (only when `original != null`, using `TableDiff.Compute`): one per `DropColumn` ("data in column X is lost"), one per `AlterColumn` with `ColumnChange.Type` ("changing the type of X may fail or lose data"), one per `AlterColumn` that turns a nullable column into a non-nullable one ("existing NULLs make this fail").
- Pure and deterministic; messages in English; issues in the rule order above.

## Out of scope
- Checking that referenced tables exist (the designer view-model does it, P7-21), reserved words (everything is quoted).

## Tasks
- [ ] Implement the validator.
- [ ] Tests in `tests/Squill.Core.Tests`: one test per error and per warning, a clean new table and a clean edit give no issue, the dialect-specific rules.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add table design validation
