# P14-03 - Sql: tables outside the default schema are inserted qualified

Status: todo
Depends on: none

## Goal
Accepting a table from the popup always gives a name the engine resolves: `audit.log` instead of `log` when `log` is not in the default schema or exists in several schemas.

## Design decisions (final)
- One internal helper, `TableReference` (`src/Squill.Sql/Completion/TableReference.cs`): `static string Insert(TableInfo table, SchemaSnapshot schema, DatabaseDialect dialect)` returns the quoted bare name when the table is in the default schema or has no schema, or when its name is unique among all tables **and** `schema.FindTable(null, name)` finds it; otherwise `quote(schema).quote(name)` (both through `IdentifierQuoting.Quote`). Rule in one sentence: bare when the engine would resolve the bare name to this table, qualified otherwise. `DefaultSchema` null (SQLite, MySQL profile without a database) means "bare when the name is unique".
- `Label` stays the bare table name; `Detail` becomes `"table"` / `"view"` as today, plus ` (schema)` when qualified is inserted (`"table (audit)"`), so two `log` tables are distinguishable; `FilterText` is the bare name.
- `SchemaCompletionProvider.Table(...)` (and nothing else) uses the helper. After `schema.` (qualifier typed) the items insert the bare name as today (the schema is already typed).
- `SqlSnippets.SelectTop` (Presentation) is unrelated, do not touch.

## Out of scope
- Join conditions (they reference tables by alias or bare name only), columns, database (catalog) qualification on SQL Server / MySQL cross-database names.

## Tasks
- [ ] Tests first (`tests/Squill.Sql.Tests/Completion/TableReferenceTests.cs` and additions in `SchemaCompletionTests`): default schema table bare; other schema table qualified; same name in two schemas, default one bare and other qualified; `DefaultSchema` null and unique name bare; two same names and no default both qualified; quoting per dialect (`[audit].[log]`, `` `audit`.`log` ``, `"Audit"."log"` on PostgreSQL with a capital); item after `audit.` is bare.
- [ ] Implement; update the existing test expectations that listed `Detail` of other-schema tables, with the reason in the commit message.

## Acceptance criteria
- Recipe section 4.

## Suggested commit message
Insert schema-qualified table names when the bare name is not enough
