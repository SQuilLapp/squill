# P14-12 - Sql: ranking by relevance (keys first, tables already used, related tables)

Status: todo
Depends on: P14-08, P14-09

## Goal
The order of the popup reflects what the user is likely to want, using only data the provider already has: key columns, tables used in the script, tables related to the FROM list.

## Design decisions (final)
- Only `SortText` values change (LoomEditor ranks first by its fuzzy score of the typed prefix, `SortText` breaks ties and orders the list when nothing is typed; `CompositeCompletionProvider` orders by `SortText` ordinal). Group prefixes after this card (lower sorts first): `"00"` relation rows and ON conditions (P14-08/09), `"01"` columns of the table nearest to the caret that are key columns (primary key, or part of a foreign key to / from another visible table) in WHERE / ON / SELECT lists, `"02"` other columns, `"03"` aliases / tables as qualifiers in column lists, `"04"` tables already used in this script (the text of `request.Text`, counted by tokens, not substrings) then tables related to a table of the FROM list through a foreign key (comma-join and `FROM` after the first source; not for `INSERT INTO` / `UPDATE`), `"05"` other tables / views, `"06"` schemas, `"07"` functions, `"08"` keywords. Within a group: name ordinal, except `"04"`: more occurrences first.
- Inside `ON`: columns that take part in a foreign key between the join's new table and an earlier source get `"01"`, everything else `"02"`.
- `KeywordCompletionProvider` and `FunctionCompletionProvider` sort texts follow the table above through one shared `internal static class CompletionOrder` holding the group constants (no magic strings left in the providers).
- Does not add history-based ranking (cross-session usage) and does not reorder what LoomEditor scored higher by prefix.
- Cost: usage is counted over the text of `request.Text` only, which the heads already slice to the statement under the caret (`LoomSqlProviders.CurrentStatement`), so "used in this script" really means "used in this statement"; say so in the XML doc and in the ADR. No cache, no scan of the whole script.

## Out of scope
- Persisted usage statistics, machine ranking, changing Loom's scorer.

## Tasks
- [ ] Tests first (`tests/Squill.Sql.Tests/Completion/RankingTests.cs`): `select * from orders o where |` puts `o.customer_id` (foreign key) and `o.id` (primary key) before `o.note`; after `from customers c, |` tables related to `customers` come before unrelated ones; a table used twice in the statement is above one used once; `insert into |` has no relatedness ordering; ON columns of the key pair first; group order constants asserted in one test; ordering stable (same input, same output).
- [ ] Implement `CompletionOrder`, switch the providers to it, update tests that asserted the old `"0"` / `"1"` / `"2"` prefixes, reason in the commit message.

## Acceptance criteria
- Recipe section 4.

## Suggested commit message
Rank completion items by keys, usage and relatedness
