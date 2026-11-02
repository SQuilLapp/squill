# P7-07 - Core: table diff into schema operations

Status: done
Depends on: P7-06

## Goal
Turn "original design" + "edited design" into an ordered list of abstract schema operations that the per-dialect generators translate.

## Design decisions (final)
- In `Squill.Core.Design`: `public abstract record SchemaOperation` and these sealed records: `RenameTable(string From, string To)`, `AddColumn(ColumnDesign Column)`, `DropColumn(string Name)`, `RenameColumn(string From, string To)`, `AlterColumn(ColumnDesign Before, ColumnDesign After, ColumnChange Changes)`, `DropPrimaryKey(string? Name)`, `AddPrimaryKey(IReadOnlyList<string> Columns)`, `CreateIndex(IndexDesign Index)`, `DropIndex(string Name)`, `AddForeignKey(ForeignKeyDesign ForeignKey)`, `DropForeignKey(string Name)`. `[Flags] public enum ColumnChange { None = 0, Type = 1, Nullability = 2, Default = 4 }`. In every `AlterColumn`, `Before.Name` and `After.Name` are both the name **after** the `RenameColumn` of the same column (so the list is replayable in order).
- `public static class TableDiff { public static IReadOnlyList<SchemaOperation> Compute(TableDesign original, TableDesign edited); }`. `original` comes from `TableDesign.FromTable`.
- Column matching is by `OriginalName` (exact). Original columns that no edited column names as `OriginalName` give `DropColumn`; edited columns with `OriginalName == null` give `AddColumn`; matched columns with a different `Name` give `RenameColumn`; matched columns whose `DataType` (trimmed, case-insensitive), `IsNullable` or `DefaultExpression` (trimmed, `null` equals empty) differ give one `AlterColumn` with the flags set. An `IsAutoIncrement` change on an existing column gives no operation (P7-08 rejects it).
- Primary key: compare the original key (original column names, in order) with the edited key mapped through `OriginalName ?? Name`; when different, emit `DropPrimaryKey(original.PrimaryKeyName)` if the original had a key and `AddPrimaryKey(edited.PrimaryKey)` (current names) if the edited one has one.
- Indexes and foreign keys are matched by `OriginalName`. An object is unchanged when uniqueness (or actions, referenced schema/table) and the column lists, mapped through `OriginalName ?? Name`, are equal: a pure column rename never recreates an index or key. Changed gives drop plus create; removed gives drop only; new gives create only (`OriginalName == null`).
- A dropped column that an original index or foreign key uses: that object is dropped by the ordered drops below, and recreated only if the edited design still lists it (with its remaining columns).
- Order of the result (stable inside each group, following list order): 1 `DropForeignKey`, 2 `DropIndex`, 3 `DropPrimaryKey`, 4 `DropColumn`, 5 `RenameColumn`, 6 `AlterColumn`, 7 `AddColumn`, 8 `AddPrimaryKey`, 9 `CreateIndex`, 10 `AddForeignKey`, 11 `RenameTable` (last, so every earlier statement uses the old table name).
- Identical designs give an empty list.

## Out of scope
- Schema moves (P7-08 rejects them), SQL text, ordering across several tables.

## Tasks
- [x] Implement the operations and `TableDiff`.
- [x] Tests in `tests/Squill.Core.Tests`: each operation alone; rename plus type change on one column; composite key change; index recreated when uniqueness flips, not recreated on a column rename; foreign-key action change; dropped column used by an index; the exact group order on a mixed edit; identical designs give an empty list.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add table diff into schema operations
