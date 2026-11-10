# P14-05 - Sql: relations between a new table and the tables already in the FROM list

Status: done
Depends on: P14-04

## Goal
A pure finder returns, for a candidate table, the ordered list of ways it can be joined to the visible tables: foreign keys in both directions, composite keys, several keys between the same pair, self references, and a same-name-column fallback. No text is built here.

## Design decisions (final)
- `internal sealed record TableRelation(VisibleSource Existing, TableInfo Other, IReadOnlyList<string> ExistingColumns, IReadOnlyList<string> OtherColumns, RelationOrigin Origin, string? ForeignKeyName, bool OtherReferencesExisting)` with `internal enum RelationOrigin { ForeignKey, SameColumnName }`; `internal static class TableRelationFinder` with `IReadOnlyList<TableRelation> Find(SchemaSnapshot schema, IReadOnlyList<VisibleSource> leftSources, TableInfo candidate)`. `ExistingColumns[i]` pairs with `OtherColumns[i]` (key order).
- Rules are those of the recipe section 2.3 and 2.4, restated for implementation: for each left source that is a real table (`DerivedColumns` null, found by `schema.FindTable(source.Schema, source.Name)`): (a) each `ForeignKeyInfo` of `candidate` whose `ReferencedTable` / `ReferencedSchema` match the source table (schema compared only when the key carries one) gives a relation with `OtherReferencesExisting = true`, `OtherColumns = fk.Columns`, `ExistingColumns = fk.ReferencedColumns`; (b) each foreign key of the source table that references `candidate` gives `OtherReferencesExisting = false` with the columns swapped. A key whose two column lists differ in length is ignored. A self-referencing key of a table that is both candidate and source produces (a) and (b) once each.
- (c) the same-name fallback applies **only when (a) and (b) found nothing for the whole call**: for each left source table, columns named X (not `id`, case-insensitive) present in both tables where X is a primary-key column of one of them; for a composite primary key all key columns must exist in the other table; one relation per source and per key, `Origin = SameColumnName`, `ForeignKeyName = null`. Views take part in (c) only as the candidate or the source with matching column names; they never produce (a) or (b) because their `ForeignKeys` are empty.
- Order of the result: strength (`ForeignKey` before `SameColumnName`), then proximity (index of the source in `leftSources`, **last first**), then `ForeignKeyName` ordinal (null last), then `OtherReferencesExisting` true first. The same candidate listed twice for one source is possible (two keys) and kept.
- Comparisons are `OrdinalIgnoreCase`. No allocation proportional to the schema size: only the foreign keys of `candidate` and of the left source tables are visited; to find (b) for a **single** candidate visit only the sources' keys (never all tables). Finding which tables to offer at all is P14-07's job.

## Out of scope
- Building text, aliases, quoting, naming heuristics such as `customer_id` -> `customers.id`.

## Tasks
- [x] Tests first (`tests/Squill.Sql.Tests/Completion/TableRelationFinderTests.cs`): child to parent (`orders` candidate, `customers` visible) and parent to child (`customers` candidate, `orders` visible) with the right `OtherReferencesExisting`; composite key order kept; two keys between `orders` and `customers` (billing and shipping) give two relations in constraint-name order; self reference (`employees.manager_id -> employees.id`, source `employees e`, candidate `employees`) gives two relations; three visible sources, the nearest (last) first; schema-qualified referenced table matched only in the right schema (`a.t` vs `b.t`); a source that is a CTE or derived table is skipped; mismatched key lengths ignored; fallback `customer_no` primary key of `customers` found in `orders` when no key exists, not used when a key exists, `id` never used; candidate unrelated gives empty; case-insensitive names.
- [x] Implement, XML docs.

## Acceptance criteria
- Recipe section 4.

## Suggested commit message
Find the ways a table can be joined to the visible tables
