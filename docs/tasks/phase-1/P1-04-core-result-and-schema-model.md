# P1-04 - Core: column, schema and diagnostic models

Status: todo
Depends on: P1-03

## Goal
Define the shared data shapes used by providers, SQL services and view-models.

## Design decisions (final)
- Namespace `Squill.Core`. All types are `sealed record` with `IReadOnlyList<T>` collections.
- `ColumnInfo(string Name, string DataTypeName, Type ClrType, bool IsNullable, int Ordinal)`. Row values are `object?[]`; SQL NULL is CLR `null`, never `DBNull`.
- Schema: `SchemaSnapshot(string? DefaultSchema, IReadOnlyList<TableInfo> Tables)`; `TableInfo(string? Schema, string Name, TableKind Kind, IReadOnlyList<ColumnDefinition> Columns, IReadOnlyList<IndexInfo> Indexes, IReadOnlyList<ForeignKeyInfo> ForeignKeys)`; `enum TableKind { Table, View }`; `ColumnDefinition(string Name, string DataType, bool IsNullable, bool IsPrimaryKey, string? DefaultExpression)`; `IndexInfo(string Name, bool IsUnique, IReadOnlyList<string> Columns)`; `ForeignKeyInfo(string Name, IReadOnlyList<string> Columns, string? ReferencedSchema, string ReferencedTable, IReadOnlyList<string> ReferencedColumns)`.
- `SchemaSnapshot.FindTable(string? schema, string name)` returns `TableInfo?`: case-insensitive on both parts; when `schema` is null it matches `DefaultSchema` first, then any schema if the name is unique; null otherwise. `SchemaSnapshot.Empty` static.
- `enum DiagnosticSeverity { Error, Warning, Information, Hint }` and `readonly record struct TextRange(int Start, int Length)` with `End` property (used by P3 and P4).

## Out of scope
- Providers (P1-05+).
- Any I/O.

## Tasks
- [ ] Add the types in `src/Squill.Core/Schema/`, `src/Squill.Core/Results/`, `src/Squill.Core/Text/`.
- [ ] Tests for `FindTable` (case, default schema, ambiguity) and `TextRange.End`.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add Core result, schema and text-range models
