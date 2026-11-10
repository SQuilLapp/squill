# P14-09 - Sql: conditions proposed inside an ON clause follow the same rules

Status: todo
Depends on: P14-05, P14-07, P14-08

## Goal
When the user typed `JOIN orders o ON ` by hand (or deleted the generated condition), the popup proposes the same conditions with the same side order, no longer lists a condition already written, and keeps working after `AND`.

## Design decisions (final)
- Replace `SchemaCompletionProvider.JoinConditions` (today: every foreign key between any two visible tables, child side first) by a version built on `TableRelationFinder` and `JoinConditionText`. Scope of a proposal in `ClauseKind.On` with no qualifier typed: the **right-most source of the join being written** is the "new" side (the table after the `JOIN` that owns this `ON`), every earlier source of the same FROM list is "existing"; resolution of which join owns the caret: the last `JOIN` keyword before the caret in the dependency-free token walk of P14-04 (reuse `JoinContextAnalyzer`'s token helpers; add an `AnalyzeOn` entry point returning the new source and `LeftSources`, with tests). Side order is the recipe's (existing first, new second), which **changes** today's child-first order: edit the existing test expectations and say so in the commit message.
- Rows: `Kind` `Snippet` as today, `Label` = condition text, `InsertText` = condition text, `Detail` = foreign key name or `same column name`, `SortText` `"00" + rank` (finder order), `Documentation` = `Describe` text. Aliases: the alias or table name each source already has (no new alias is generated here).
- **Not proposed**: a condition already present in the clause (compare the normalized token sequence of each `a.x = b.y` conjunct of the existing `ON` expression, either side order, case-insensitive), so after `ON c.id = o.customer_id AND ` only the remaining relations appear. If the caret is after `AND` / `OR` the same rows are valid; after a complete condition with no operator the popup shows keywords as today.
- Nothing is proposed when `LeftSources` has no real table, the schema is null, or the join is `CROSS` / `NATURAL` (no `ON` can follow; the keyword provider already does not offer `ON` there after P14-13, but guard here too).
- Column proposals in `ON` are unchanged except for the ordering of P14-12.

## Out of scope
- `USING (...)` proposals, conditions between non-adjacent joins in a chain beyond "every earlier source".

## Tasks
- [ ] Tests first (`tests/Squill.Sql.Tests/Completion/OnClauseTests.cs`): `from customers c join orders o on |` gives `c.id = o.customer_id` first; same with an inner / left / right / full join (identical rows); composite key; two keys between the pair give two rows; second join `... join order_items oi on |` offers relations of `oi` to **both** `o` and `p` (when `products p` is earlier) ranked nearest first and never a relation between `c` and `o` again; after `on c.id = o.customer_id and |` the already written condition is gone and the next relation (if any) is there; same condition written reversed (`o.customer_id = c.id`) also counts as present; no alias sources use table names; quoting per dialect.
- [ ] Update the old `JoinConditions` tests (child-first expectations) to the new order, reason in the commit message.
- [ ] Implement, XML docs.

## Acceptance criteria
- Recipe section 4.

## Suggested commit message
Make ON clause proposals follow the same side order and skip written conditions
