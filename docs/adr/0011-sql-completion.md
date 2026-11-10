# ADR 0011 - SQL completion: join conditions and a better popup

- Status: accepted
- Date: 2026-11-10

## Context
Before phase 14 (cards P14-01 to P14-19, [task index](../tasks/README.md)) the popup proposed tables, columns, keywords and functions by name only: picking a table after `JOIN` inserted the bare name (no alias, no condition), a table outside the default schema was inserted unqualified, a column shared by two sources was inserted unqualified, keywords were always upper case, `FULL JOIN` was offered on MySQL, there was no documentation text and no statement snippets, and the popup did not open by itself after `JOIN `. Only inside an `ON` clause did the provider propose the foreign-key conditions. The audit is in [completion-recipe.md](../tasks/phase-14/completion-recipe.md) section 1; the evidence and the owner's checklist are in [verification/completion.md](../verification/completion.md).

## Decision
- **All intelligence is in `Squill.Sql/Completion`** (pure, AOT-safe, no UI type). The heads only map `CompletionItem` fields to LoomEditor's (`src/Squill.App.Shared/LoomSqlProviders.cs`, linked into both heads; the plain-text fallback editor uses `SnippetText.ToPlain` and has no documentation panel). `QueryTabViewModel`'s `SessionCompletionProvider` adds the session schema and the keyword-case setting, and merges the keyword, schema, function and snippet providers.
- **Join rows above the unchanged table list.** After `JOIN ` the popup shows first one row per relation between the new table and a table already in the FROM list (`orders o`, detail `ON c.id = o.customer_id`, inserted text `orders o ON c.id = o.customer_id`, filter text the bare table name), then the plain table, view and schema rows as before. A table with no relation only gets its plain row.
- **Relations** (`TableRelationFinder`), by strength: a foreign key of the new table to a visible one, a foreign key of a visible table to the new one, then (only when none exists) a same-named non-`id` primary-key column present in the other table. Several keys between a pair give several rows, a composite key is one row joined by `AND`, a self reference gives two rows, a repeat of a table is still offered. Ranking: strength, source closest to the caret, new before repeated table, table name, constraint name.
- **Side order of `ON`: the table already in the FROM list first, the new table second, for every join kind.** `=` is symmetric, so the written order cannot change the rows of `INNER`, `LEFT`, `RIGHT` or `FULL`; the join keyword decides which side is preserved. One rule that follows the FROM list is easier to read and to test than four; it is a single switch in `JoinConditionText` if the owner prefers otherwise.
- **Which joins get an `ON`**: plain, `INNER`, `LEFT`, `RIGHT`, `FULL` (with or without `OUTER`). `CROSS`, `NATURAL`, comma, `APPLY` and a join whose next token is already `ON` / `USING` get the plain table only; the conditions of the `ON` clause skip one already written. The join keywords offered follow the engine (no `FULL JOIN` on MySQL, `APPLY` on SQL Server and Oracle, `LATERAL` on PostgreSQL).
- **Alias policy** (`AliasGenerator`): an alias only when another source of the FROM list has one, the table is already in it, or the relation is a self reference; lower-case initials of the name's words, extended on a collision with a source name, alias or reserved word; never `AS` (Oracle forbids it).
- **Quoting** goes through `IdentifierQuoting.Quote` per identifier. **Qualified insertion** (`TableReference`): a table is written bare only when the engine resolves the bare name to it, that is it is in the default schema, or the engine has no default schema and the name is unique; otherwise `schema.table`. A column name shared by two visible sources is inserted `alias.column`.
- **One undo step**: LoomEditor pushes an undo stop before and after an accepted item and the join row is one plain insertion, so one Ctrl+Z reverts name, alias and condition together; the caret ends after the condition.
- **Auto-open**: the popup opens after a space typed after `FROM`, `JOIN`, `INTO`, `UPDATE`, `ON`, `USING`, `SELECT` or a comma in a FROM list (`CompletionTrigger.OpensAfterSpace`; trigger characters `.` and space).
- **Ranking groups** (`CompletionOrder`): join rows and ON conditions, key columns of the table nearest the caret, other columns, qualifiers, tables already used or related, other tables, schemas, functions, keywords, snippets.
- **Documentation text** (`CompletionDocumentation`) for tables, columns, relations and functions; **keyword case** follows the `FormatterKeywordCase` setting (`Preserve` follows the script); functions are in the session composite; **statement snippets** `sel`, `ins`, `upd`, `del`, `cte`, `crt` with tab stops, only where a statement can start (the `cte` template repeats `${1:name}`, which LoomEditor's Monaco-derived parser mirrors; read at tag `v0.5.0`).

## Alternatives considered
- **A snippet with placeholders to choose the foreign key.** Rejected: one row per key is faster to pick and shows the condition before accepting.
- **A side order that depends on the join kind.** Rejected: no semantic effect, four rules to read and test.
- **A second popup chained after the table (item commands).** Not possible: LoomEditor 0.5.0 does not support item commands.
- **Generating the condition as a second step after Tab.** Rejected: two undo steps and a hidden state.
- **A bare name when it is unique among all tables.** Tried in P14-03, disproved by the PostgreSQL scenario of P14-17: an engine with a default schema does not look in the others.

## Consequences
- The generated text is executed on SQLite, PostgreSQL, MySQL and SQL Server by one scenario (`tests/Shared/JoinCompletionScenario.cs`; SQLite runs everywhere, the others in Podman or Docker); the query tab path is covered by `CompletionFlowTests`.
- A request stays interactive on 5,000 tables with 10,000 foreign keys (relation search starts from the foreign keys of visible tables; guarded by a test with generous bounds).

## Known limits
- No naming heuristic such as `customer_id` -> `customers.id`; the same-name fallback is conservative.
- Usage ranking looks at the current statement only. A keyword-case change reaches the popup after up to 5 seconds.
- Whether to generate the condition cannot be turned off; `USING` is never generated; derived tables and CTEs have no relations.
- Oracle goes through the same code and was not verified live (no Oracle container in phase 14).
- Popup detail strings are not translated (SQL keywords never are); the plain-text fallback editor shows no documentation.
- **The popup was not driven by hand in either head by an agent session**: the checklist of [verification/completion.md](../verification/completion.md) is open.
