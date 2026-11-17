# P18-05 - Core: schema comparison model

Status: done
Depends on: none

## Goal
A pure function that compares two `SchemaSnapshot`s of the same dialect and lists what differs, grouped the way M09 groups it (`TABLES`, `VUES`, `INDEX`, `FONCTIONS`). Recipe 1.

## Design decisions (final)
- `src/Squill.Core/Schema/Compare/`: `SchemaComparer.Compare(SchemaSnapshot source, SchemaSnapshot target)` returns `SchemaComparison` (`IReadOnlyList<ObjectDifference> Differences`, counts per `DifferenceKind` `Added | Removed | Modified`). "Source" is the desired state, "target" is what the script will change: `Added` = in source only (to create on target), `Removed` = in target only (to drop), matching the chips `Ajoutés`, `Supprimés`, `Modifiés`.
- `ObjectDifference(ObjectCategory Category, string? Schema, string Name, DifferenceKind Kind, IReadOnlyList<DifferenceDetail> Details)`; categories `Table`, `View`, `Index`, `Routine` (functions and procedures; triggers are compared as part of their table's details only if `TriggerInfo` allows a stable text compare, else not compared and listed in Out of scope in the report). `DifferenceDetail(DetailKind, string Subject, string? SourceText, string? TargetText)` with kinds `ColumnAdded | ColumnRemoved | ColumnChanged | PrimaryKeyChanged | ForeignKeyAdded | ForeignKeyRemoved | ForeignKeyChanged | IndexAdded | IndexRemoved | IndexChanged | DefinitionChanged`. The row text of M09 (`4 changements`, `1 colonne`, `nouvelle`, `définition`) is derived by the Presentation card from `Details`, not stored here.
- Matching: names case-insensitive per dialect rules already used by `SchemaSnapshot.FindTable`; columns by name (no rename detection: a rename is a removed + an added column; say so in the XML doc); a column is changed when type, nullability, default, identity or comment differs (use the equality the designer's `TableDiff` uses; read it first and reuse its notion of "changed" so compare and designer agree). Views and routines: changed when `Definition` text differs after whitespace normalization; when either definition is null they are reported `Modified` only if the other is not null, and flagged `DefinitionUnknown`.
- Column order differences are never reported (no engine here can reorder columns in place), so the M09 checkbox `Ignorer espaces et ordre des colonnes` is **not drawn**; definition whitespace is always normalized. No options type is needed: `Compare(source, target)`. Update recipe 1 and P18-08 accordingly if you find a reason to differ, and say so in the report.
- Order of the result: category, then schema, name. Deterministic.
- Dialect mismatch is the caller's error; the comparer takes no dialect (identifier comparison is case-insensitive, ordinal).

## Out of scope
- Script generation (P18-06), data compare, rename detection, UI.

## Tasks
- [x] Model and comparer, XML doc (no options type, per the design decisions).
- [x] Tests in `tests/Squill.Core.Tests/Schema/`: identical snapshots give none, one table only in source / target, column added / removed / changed (type, nullability, default), primary key change, foreign key change, index added, view definition changed with whitespace only (not reported) and real (reported), routine changed, null definitions, case-insensitive names, deterministic order, 5,000 tables compare under a second.

## Acceptance criteria
- Common acceptance (recipe 7), no head touched.

## Suggested commit message
Add the schema comparison model
