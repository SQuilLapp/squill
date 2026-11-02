# P7-15 - Presentation: ER model built from a schema snapshot

Status: todo
Depends on: P7-01, P4-05

## Goal
The pure entity-relationship model (entities, columns, relations) of a `SchemaSnapshot`, ready to be laid out and drawn.

## Design decisions (final)
- Namespace `Squill.Presentation.Diagram`, folder `src/Squill.Presentation/Diagram/`. Types (public sealed records unless noted, XML doc on all):
  - `ErColumn(string Name, string DataType, bool IsPrimaryKey, bool IsForeignKey, bool IsNullable)`.
  - `ErEntity(string Key, string? Schema, string Name, TableKind Kind, IReadOnlyList<ErColumn> Columns)`; `Key` is `Name` when the schema is null, else `Schema + "." + Name`; keys are compared with `StringComparer.OrdinalIgnoreCase`.
  - `enum ErCardinality { ManyToOne, OneToOne }`.
  - `ErRelation(string Name, string FromKey, IReadOnlyList<string> FromColumns, string ToKey, IReadOnlyList<string> ToColumns, ErCardinality Cardinality, bool IsOptional)`: `FromKey` is the table that holds the foreign key (child), `ToKey` the referenced table (parent). `Cardinality` is `OneToOne` when the foreign-key columns are exactly the primary key or exactly the columns of a unique index of the child; `IsOptional` when every foreign-key column is nullable.
  - `ErModel(IReadOnlyList<ErEntity> Entities, IReadOnlyList<ErRelation> Relations, int UnresolvedRelations)` with `ErEntity? Find(string key)`.
- `ErModel.Build(SchemaSnapshot snapshot, ErFilter? filter = null)` and `ErModel.Neighborhood(ErModel model, string key, int depth)` (returns the set of keys within `depth` relation hops of `key`, both directions, including `key`).
- `sealed record ErFilter(IReadOnlySet<string>? Keys, bool IncludeViews = false)`: `Keys == null` means all entities; views are skipped unless `IncludeViews`; a view never has relations.
- Entities are ordered by schema then name (ordinal, case-insensitive). A foreign key whose referenced table is missing from the model (not in the snapshot or filtered out) is counted in `UnresolvedRelations` and not emitted. The referenced schema defaults to the child's schema when `ForeignKeyInfo.ReferencedSchema` is null (resolve with `SchemaSnapshot.FindTable`, falling back to the child's schema first). Self-references are kept.
- Pure, no UI types, no I/O.

## Out of scope
- Layout (P7-16), routing (P7-17), view-model (P7-19).

## Tasks
- [ ] Implement the model and builder.
- [ ] Tests in `tests/Squill.Presentation.Tests`: single table, a foreign key (many-to-one, optional vs required), one-to-one through a unique index and through the primary key, composite foreign key, cross-schema relation, self-reference, filter by keys, view handling, unresolved relation counted, `Neighborhood` depths 0, 1, 2.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add ER model built from the schema snapshot
