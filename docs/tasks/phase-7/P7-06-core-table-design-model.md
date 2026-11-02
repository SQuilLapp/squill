# P7-06 - Core: table design model

Status: todo
Depends on: P7-01

## Goal
The editable draft of a table, independent of any UI and of any dialect, that the designer edits and the DDL generators consume.

## Design decisions (final)
- Namespace `Squill.Core.Design`, folder `src/Squill.Core/Design/`, one file per type, all `public sealed record` with XML doc:
  - `ColumnDesign(string? OriginalName, string Name, string DataType, bool IsNullable, string? DefaultExpression, bool IsAutoIncrement)`; `OriginalName` is `null` for a column added in the editor, otherwise the name it had in the database (this is how renames are detected).
  - `IndexDesign(string? OriginalName, string Name, bool IsUnique, IReadOnlyList<string> Columns)`.
  - `ForeignKeyDesign(string? OriginalName, string Name, IReadOnlyList<string> Columns, string? ReferencedSchema, string ReferencedTable, IReadOnlyList<string> ReferencedColumns, ReferentialAction OnDelete, ReferentialAction OnUpdate)`.
  - `TableDesign(string? Schema, string Name, IReadOnlyList<ColumnDesign> Columns, IReadOnlyList<string> PrimaryKey, string? PrimaryKeyName, IReadOnlyList<IndexDesign> Indexes, IReadOnlyList<ForeignKeyDesign> ForeignKeys)`; `PrimaryKey` lists the **current** column names in key order; `PrimaryKeyName` is the database name of the existing constraint (`null` for a new key or when the engine has none).
- `TableDesign.FromTable(TableInfo t)` (static): every `OriginalName` equals the name; `PrimaryKey` = the columns with `IsPrimaryKey`, in column order; `DefaultExpression` is `null` when the column `IsAutoIncrement`; `PrimaryKeyName` copied; foreign-key actions copied. Throws `ArgumentException` for a view.
- `TableDesign.New(string? schema, string name)` (static): no columns, no key, no index, no foreign key.
- Collections are never `null`. Do not override record equality (lists compare by reference); the diff (P7-07) compares explicitly.
- Nothing in this card validates; that is P7-08.

## Out of scope
- Diffing (P7-07), validation (P7-08), SQL.

## Tasks
- [ ] Implement the types.
- [ ] Tests in `tests/Squill.Core.Tests`: `FromTable` for a table with a composite key, an auto-increment column (default dropped), an index and a foreign key with actions; `FromTable` of a view throws; `New` is empty.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add table design model
