# P14-08 - Sql: picking a table after JOIN generates the ON condition

Status: todo
Depends on: P14-03, P14-04, P14-05, P14-06, P14-07, P14-01

## Goal
After `JOIN ` the popup lists, above the usual tables, one row per relation (`orders o` / `ON o.customer_id = c.id`); accepting it writes table, alias and condition in one step.

## Design decisions (final)
- Work in `SchemaCompletionProvider.GetCompletions`, in the `tablePosition` branch, after the existing `Tables(...)` call. Compute `JoinContextAnalyzer.Analyze(request, start, offset)` (the provider's `range`). Generate relation rows **only when** `IsJoin`, `Kind` is not `Cross` / `Comma`, not `IsNatural`, not `IsApply`, not `HasCondition`, no qualifier was typed before the caret (`qualifiers.Count == 0`; after `schema.` the schema is fixed, but relations are still offered for tables of that schema: pass the schema filter to the candidate list), and `LeftSources` has at least one real table.
- Candidates: every table or view of the snapshot (restricted to the qualifier's schema when one was typed). For each, `TableRelationFinder.Find(schema, LeftSources, candidate)`; to avoid a full scan per request, first build the set of "related table keys": names referenced by the foreign keys of the left tables plus tables whose foreign keys reference a left table (one pass over `schema.Tables` reading only `ForeignKeys`), and run the finder only on those plus, when that set is empty for a source, on tables sharing a primary-key column name (fallback c). Guard: a snapshot of 5,000 tables / 10,000 keys answers in well under 200 ms in a test.
- Row per relation: `Label` = `quote(table[ alias])` where the table part is what P14-03's `TableReference.Insert` returns minus nothing (qualified when needed) and the alias is added when `AliasGenerator.IsAliasNeeded(...)` (selfReference = the candidate is also the existing source of that relation); `taken` for the generator = the aliases and names of all left sources (rows are alternatives, not a sequence, so aliases given to other rows of the same call do not count). `InsertText` = `Label + " ON " + JoinConditionText.Build(relation, existingAliasOrName, newAliasOrName, dialect)`; `Detail` = `"ON " + condition`; `FilterText` = bare table name; `Kind` = `Struct`; `SortText` = `"00" + zero-padded rank` (rank = position in the order across all candidates: strength, proximity, **repeat last** (a candidate already in the left list sorts after those that are not), then table name ordinal, then key name ordinal); `Documentation` = `JoinConditionText.Describe(relation)` plus, for a foreign key, ` ON DELETE <action>` using `ReferentialActions` text (look at how the designer prints it).
- Plain rows stay exactly as P14-03 leaves them (`SortText` `"0" + name`, so they sort after `"00..."`).
- The existing-source name used in the condition: its alias, else its bare table name (not schema-qualified, valid on all four engines). If two left sources have the same name without alias the relation uses the nearest one only (the finder order already prefers it).
- Edge cases to implement and test: nothing is generated when `request.Schema` is null; when the only left sources are derived tables or CTEs; when the table list is empty; a candidate whose relation exists to two left sources gives two rows (ranked by proximity).

## Out of scope
- The ON clause itself (P14-09), opening the popup automatically (P14-10), ranking plain rows (P14-12), documentation for plain rows (P14-15).

## Tasks
- [ ] Tests first (`tests/Squill.Sql.Tests/Completion/JoinRowsTests.cs`, schema with `customers`, `orders` (fk to customers), `order_items` (composite fk to orders, fk to products), `products`, `employees` (self key), `tags` (no key)): `from customers c join |` first row `orders o` / `ON c.id = o.customer_id` with `InsertText` `orders o ON c.id = o.customer_id`; `from customers join |` (no alias on source) gives `orders ON customers.id = orders.customer_id` with **no** alias; `from customers c join orders o on c.id = o.customer_id join |` gives, in this order, `order_items oi` / `ON o.id = oi.order_id` (new table, nearest source `o`) then the repeat row `customers cu` / `ON o.customer_id = cu.id` (a table already in the list is offered after the tables that are not, alias `cu` because `c` is taken); composite key `ON o.id = oi.order_id AND o.line = oi.order_line`; two keys between the same pair give two rows; self join `from employees e join |` gives `employees em` twice (`ON e.id = em.manager_id` and `ON e.manager_id = em.id`); `left join |`, `right join |`, `full outer join |`, `inner join |` all generate with the **same** side order; `cross join |`, `natural join |`, `, |`, `cross apply |` generate nothing; caret before an existing `on` generates nothing; unrelated table `tags` has only the plain row; MySQL quoting, SQL Server brackets, PostgreSQL capitals, schema-qualified candidate; accept simulation: apply `ReplaceRange` + `InsertText` to `... join ord|` and assert the whole text; the 5,000-table guard.
- [ ] Implement, XML docs. Update the existing `SchemaCompletionTests` cases that listed the exact items of a `join |` position (join rows now precede the plain rows) with the reason in the commit message.

## Acceptance criteria
- Recipe section 4. `rtk bypass dotnet test --solution Squill.slnx -c Release` green.

## Suggested commit message
Generate the ON condition when a table is picked after JOIN
