# P20-04 - Sql: the order of a multi-table drop and the tables that block it

Status: todo
Depends on: none

## Goal
Dropping several tables needs two answers computed from the schema the app already holds: in which order the statements must run (a table that references another goes first), and which tables **outside** the selection reference a selected table (the engine would refuse, and `CASCADE` is never used). Pure logic, no UI, no connection (recipe 4 of [verification-recipe.md](verification-recipe.md)).

## Design decisions (final)
- New file `src/Squill.Sql/Ddl/TableDropPlan.cs` (namespace `Squill.Sql.Ddl`, same assembly as `DdlGenerators`; `Squill.Sql` references `Squill.Core`, which owns `TableInfo`, `ForeignKeyInfo`, `SchemaSnapshot`):
  - `public sealed record DropBlocker(TableInfo Referencing, TableInfo Referenced)`: `Referencing` is not selected and has a foreign key to `Referenced`, which is.
  - `public sealed record TableDropPlan(IReadOnlyList<TableInfo> Order, IReadOnlyList<DropBlocker> Blockers, bool HasCycle)`.
  - `public static TableDropPlan Build(IReadOnlyList<TableInfo> selected, SchemaSnapshot schema)`.
- Table identity: schema (null = the schema of the table that owns the foreign key: `ForeignKeyInfo.ReferencedSchema` is null on engines without schemas, and MySQL puts the database name in `TableInfo.Schema`, see P18-33) and name, compared with `StringComparison.OrdinalIgnoreCase` (SQLite and MySQL are case-insensitive; a PostgreSQL schema with two tables differing only by case is a known limit, write it as a `shortcut:` comment with "upgrade when ...").
- A foreign key of a table to itself is ignored. Duplicates in `selected` are removed (first wins). Views and any `TableInfo.Kind != TableKind.Table` in `selected` throw `ArgumentException` (the caller filters; a wrong kind must not become a `DROP TABLE` of a view).
- `Order`: Kahn's algorithm over the foreign keys **between selected tables**, a referencing table before the table it references, ties broken by the order of `selected` (stable). On a cycle the remaining tables are appended in `selected` order and `HasCycle` is true.
- `Blockers`: every non-selected table of `schema.Tables` with a foreign key to a selected table, ordered by referencing schema and name, one entry per (referencing, referenced) pair.
- Public type and members have XML documentation (no member name repetition); `Build` has `<exception>`.

## Out of scope
- Views, routines, triggers that depend on a table (the engine reports them; P20-05 shows the engine message), executing anything.

## Tasks
- [ ] Tests first, `tests/Squill.Sql.Tests/TableDropPlanTests.cs`, names `Method_Scenario_Expected`: no foreign keys keeps the selection order; `commandes` -> `clients` selected as `[clients, commandes]` gives `[commandes, clients]`; a chain of three; a diamond is stable; a cycle sets `HasCycle` and keeps selection order; a foreign key to a table outside the selection gives one blocker; a self reference is ignored; null `ReferencedSchema` resolves to the owner's schema (MySQL shape); case-insensitive match; a view in the selection throws; duplicates removed. Run them: they fail (type missing).
- [ ] Implement until green.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` 0 warnings, `rtk bypass dotnet test --solution Squill.slnx -c Release` green, XML doc grep check.

## Risks
- `ForeignKeyInfo` fields: read `src/Squill.Core/Schema/ForeignKeyInfo.cs` before writing; if a reader fills `ReferencedSchema` differently on an engine, the test data must copy that shape (check `tests/Squill.Data.*.Tests` sample schemas) rather than invent one.

## Suggested commit message
Plan the order of a multi-table drop and the tables that block it
