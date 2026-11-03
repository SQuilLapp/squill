# P11-15 - Core: routines, triggers, view definitions and materialized views in the schema model

Status: todo
Depends on: none

## Goal
`SchemaSnapshot` can describe what the explorer cannot show today: functions and procedures, triggers, view definitions, materialized views. The model only has tables and views (`TableKind { Table, View }`), no routines, no triggers, no definition text.

## Design decisions (final)
- `TableKind` gets `MaterializedView` (PostgreSQL only). Check every `switch` on `TableKind` in `src/` and decide for each (editable: no; ER diagram: excluded like views).
- `TableInfo` gets a defaulted trailing `string? Definition = null` (the `SELECT` of a view; null for tables).
- New records in `Squill.Core/Schema`: `RoutineKind { Function, Procedure }`, `RoutineInfo(string Schema, string Name, RoutineKind Kind, string Signature, string? ReturnType, string? Definition)` (Signature = `name(arg types)` as the engine reports it), `TriggerInfo(string Schema, string Table, string Name, string Timing, string Events, string? Definition)`.
- `SchemaSnapshot` gets defaulted `IReadOnlyList<RoutineInfo> Routines = []` and `IReadOnlyList<TriggerInfo> Triggers = []` (no break for callers; `SchemaSnapshot.Empty` unchanged). XML doc everywhere. Snapshot equality in existing tests must still pass.
- Dialect support is documented in the `<remarks>` of the types: SQLite has triggers and no routines; the other three have both.

## Out of scope
- Readers (P11-16 ... P11-19), UI (P11-20 ...), sequences, user-defined types, synonyms (recorded as not supported in the ADR of P11-40).

## Tasks
- [ ] Types, `TableKind` switch audit, tests (construction, defaults, `FindTable` unaffected).

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` 0 warnings; `rtk bypass dotnet test --solution Squill.slnx -c Release` green.

## Suggested commit message
Model routines, triggers and view definitions in the schema snapshot
