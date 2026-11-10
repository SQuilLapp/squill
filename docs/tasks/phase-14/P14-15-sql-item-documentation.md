# P14-15 - Sql: documentation text for tables, columns, aliases, functions and join rows

Status: done
Depends on: P14-02, P14-08, P14-09

## Goal
The side panel of the popup explains the highlighted item: a table's columns and keys, a column's type and key role, what an alias stands for, a function's signature, why a join row is proposed.

## Design decisions (final)
- Fill `CompletionItem.Documentation` (P14-01; shown by both heads after P14-02 through LoomEditor's `Documentation` member, rendered as Markdown) in the providers; one `internal static class CompletionDocumentation` in `src/Squill.Sql/Completion/` builds every text so wording is in one place and tests are pure. Helper `Code(string)` wraps an identifier or SQL fragment in backticks, using a longer fence when the text contains a backtick, and escapes nothing else (identifiers only appear inside code spans).
- Texts (English, same language rule as the existing `Detail` literals; see the recipe section 4 on text):
  - **Table / view**: first line `**<name>** - table` / `view` (plus `, <n> columns`), then a code list of at most 12 columns `name type [PK] [NOT NULL]` (`...and N more` after 12), then `Foreign keys:` with at most 5 entries `name: cols -> table(cols)`, then for a view the first 12 lines of `Definition` in a fenced block when it is not null.
  - **Column**: `**<table>.<column>**`, type, `nullable` or `not null`, `primary key`, `auto increment`, `generated`, `default <expr>` when set, and `references table(column)` / `referenced by table(column)` for foreign keys (first three each).
  - **Alias / table qualifier**: `alias of <schema.table>`; a derived table / CTE: `derived table, columns: a, b, c` (first 12).
  - **Function**: `FunctionSignature.Display` then `Description`; stored function: `Signature` and return type.
  - **Join row (P14-08) and ON condition (P14-09)**: `JoinConditionText.Describe` plus, for a foreign key, `ON DELETE <action>, ON UPDATE <action>` (reuse the action text of the designer / `ReferentialActions`, check how it prints).
- Size: every text is at most 2,000 characters (truncate with `...`), so a 500-column table cannot make the popup slow.
- Documentation is built eagerly for every returned item (the providers return everything and the editor filters; `CanResolve` is not wired in the heads). Keep each text proportional to one table (the caps above) and measure: a table-position request on the 5,000-table schema of P14-08's guard must stay under the same bound. If it does not, build the text only for items whose `Label` starts with the typed prefix and say so in the report.

## Out of scope
- Resolving documentation lazily through LoomEditor's `ResolveCompletionItemAsync`, localization of the texts, keyword documentation.

## Tasks
- [x] Tests first (`tests/Squill.Sql.Tests/Completion/CompletionDocumentationTests.cs`): exact text of a table with 3 columns and a foreign key, a table with 20 columns (12 + `...and 8 more`), a view with a definition, a column with default and foreign key, a column that is referenced by two tables, an alias, a CTE, a built-in and a stored function, a join row with `ON DELETE CASCADE`, the 2,000 character cap, a name containing a backtick.
- [x] Wire into `SchemaCompletionProvider`, `FunctionCompletionProvider`, relation and condition rows; the performance guard; XML docs.

## Acceptance criteria
- Recipe section 4.

## Suggested commit message
Add documentation text to completion items
