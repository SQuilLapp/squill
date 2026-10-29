# P3-11 - Sql: table and column completion

Status: todo
Depends on: P3-10

## Goal
Complete tables, columns and aliases from the loaded `SchemaSnapshot`.

## Design decisions (final)
- `SchemaCompletionProvider : ISqlCompletionProvider` and `CompositeCompletionProvider(params ISqlCompletionProvider[])` (concatenates, orders by `SortText`).
- After `FROM`/`JOIN`/`INTO`/`UPDATE`: tables and views (Kind `Struct`, `Detail` = `table`/`view`), schemas (Kind `Module`) when more than one schema exists; after `schema.` only that schema's tables.
- Select list, WHERE, ON, GROUP BY, ORDER BY, SET: columns of every visible source (Kind `Field`, `Detail` = data type, `SortText` `0` + name for columns, `1` for aliases/tables, `2` for keywords/functions); after `alias.` or `table.` only that source's columns; CTE/derived sources use `DerivedColumns`; unknown (`null`) derived columns yield nothing.
- When a column name exists in several visible sources, `Label` stays the bare name and `Detail` shows `table.type`. Insert text is unquoted unless the name is not a plain identifier (`[a-z_][a-z0-9_]*` after lowercasing), then quoted per dialect (`"x"`, `` `x` ``, `[x]`).

## Out of scope
- Function completion (P3-13).

## Tasks
- [ ] Implement both providers and the quoting helper.
- [ ] Tests with a hand-built `SchemaSnapshot`: each context above, aliases, CTE columns, qualified access, quoting per dialect.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add schema-aware completion
