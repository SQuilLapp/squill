# P14-07 - Sql: the text of a join condition

Status: done
Depends on: P14-05, P14-06

## Goal
One pure builder turns a `TableRelation` plus the names chosen for both sides into the `ON` text, with the side order, quoting and composite handling of the recipe, so the popup (P14-08, P14-09) and the tests share one implementation.

## Design decisions (final)
- `internal static class JoinConditionText` in `src/Squill.Sql/Completion/` with `string Build(TableRelation relation, string existingName, string newName, DatabaseDialect dialect)` returning `existing.c1 = new.d1 AND existing.c2 = new.d2` (no `ON` keyword; the caller adds it). `existingName` / `newName` are the **unquoted** alias or table name; the builder quotes every identifier with `IdentifierQuoting.Quote`. **Side order is fixed: existing first, new second, for every join kind and both key directions** (recipe section 2.5): `ExistingColumns[i]` is always on the left, `OtherColumns[i]` on the right, `=` with single spaces, `AND` upper case, keys in key order. The case of `AND` follows the keyword-case setting only after P14-14 (that card changes the signature then).
- `internal static string Describe(TableRelation relation)` returns the short human text used in documentation and tests: `orders.customer_id -> customers.id (fk_orders_customer)` for a foreign key (referencing side first, so the arrow always follows the key), `same column name: customer_no` for the fallback.
- Nothing here decides whether a condition is wanted (join kind, existing `ON`): that is the provider's job (P14-08).

## Out of scope
- Aliases (P14-06), choosing relations (P14-05), items and sorting (P14-08).

## Tasks
- [x] Tests first (`tests/Squill.Sql.Tests/Completion/JoinConditionTextTests.cs`): single key both directions (existing `customers c`, new `orders o`, `OtherReferencesExisting` true: `c.id = o.customer_id`; existing `orders o`, new `customers c`, `OtherReferencesExisting` false: `o.customer_id = c.id`), composite key (`a.x = b.x AND a.y = b.y`, order kept), table name instead of alias, quoting per dialect (a column `Order Id` on MySQL and SQL Server, a capitalised name on PostgreSQL, a name containing the closing quote character), `Describe` for both origins.
- [x] Implement, XML docs.

## Acceptance criteria
- Recipe section 4.

## Suggested commit message
Build the text of generated join conditions
