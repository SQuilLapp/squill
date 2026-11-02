# P7-01 - Core: schema model extension (auto-increment, referential actions, key name)

Status: done
Depends on: P1-04

## Goal
Make the schema snapshot rich enough to edit tables faithfully: auto-increment columns, foreign-key actions and the primary-key constraint name. Only the model changes here; the readers are filled in by P7-02 to P7-05.

## Design decisions (final)
- Every new member is a **trailing optional parameter** of the existing positional record, so no existing caller or test changes: `ColumnDefinition(..., string? DefaultExpression, bool IsAutoIncrement = false)`, `ForeignKeyInfo(..., IReadOnlyList<string> ReferencedColumns, ReferentialAction OnDelete = ReferentialAction.NoAction, ReferentialAction OnUpdate = ReferentialAction.NoAction)`, `TableInfo(..., IReadOnlyList<ForeignKeyInfo> ForeignKeys, string? PrimaryKeyName = null)`. Update the `<param>`-style summaries.
- New `public enum ReferentialAction { NoAction, Restrict, Cascade, SetNull, SetDefault }` in `src/Squill.Core/Schema/ReferentialAction.cs`, namespace `Squill.Core`, XML doc on every member.
- New `public static class ReferentialActions` (same folder) with extension `string ToSql(this ReferentialAction action)` returning `NO ACTION`, `RESTRICT`, `CASCADE`, `SET NULL`, `SET DEFAULT`, and `ReferentialAction? Parse(string? sql)` (case-insensitive, trims and collapses inner whitespace, `null` for unknown or `null` input).
- `IsAutoIncrement` means "the engine generates the value": identity/serial (PostgreSQL, SQL Server), `AUTO_INCREMENT` (MySQL), rowid alias (SQLite). For such a column `DefaultExpression` is whatever the engine reports; consumers (P7-06) ignore it when `IsAutoIncrement` is true.
- `PrimaryKeyName` is `null` when the engine has no meaningful name (SQLite, MySQL) or the table has no primary key.

## Out of scope
- Reading the new values from a database (P7-02 to P7-05), check/unique constraints, triggers.

## Tasks
- [x] Add the enum, the extension class and the three trailing parameters.
- [x] Grep every `new ColumnDefinition(`, `new ForeignKeyInfo(`, `new TableInfo(` and any `with` use: the solution must still compile unchanged. If a source-generated JSON context serializes these records, keep it compiling.
- [x] Tests in `tests/Squill.Core.Tests`: `ToSql` for each member, `Parse` for each SQL spelling plus `"no   action"`, unknown and `null`, defaults of the new parameters.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Extend the schema model with auto-increment, referential actions and key name
