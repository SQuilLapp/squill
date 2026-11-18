# P19-01 - Quote reserved words in `IdentifierQuoting`

Status: done
Depends on: none

## Goal
`IdentifierQuoting.Quote` (`src/Squill.Sql/Completion/IdentifierQuoting.cs:12`) only quotes a name that is not a plain `[a-z_][a-z0-9_]*` word (and, on PostgreSQL, a name with capitals). A table named `order`, `group` or `user`, or a role named `user`, is written bare. Completion then inserts `select * from order` (invalid SQL) and `PostgresPrivilegeScript` writes `GRANT SELECT ON public.order TO user;`. The code records it: `// shortcut: reserved words (a role named "user") are not quoted` in `src/Squill.Sql/Security/PostgresPrivilegeScript.cs:29`. Callers of `IdentifierQuoting.Quote`: `JoinConditionText`, `JoinRows`, `SchemaCompletionProvider`, `TableReference`, `FunctionCompletionProvider`, `PostgresPrivilegeScript`. The DDL generators use `SqlQuoting` (always quoted) and are not affected.

## Design decisions (final)
- A name that is a keyword of the dialect is quoted. Over-quoting a valid name is harmless; under-quoting is the bug, so the rule is "any keyword of that dialect", not "reserved only" (no per-engine reserved/non-reserved table to get wrong).
- Reuse the keyword sets the tokenizer or the completion code already has (`rtk grep -rn "Keywords" src/Squill.Sql`; `AliasGenerator` already avoids reserved words: read it first). Add a new set only for a dialect that has none; one `IReadOnlySet<string>` per dialect with `StringComparer.OrdinalIgnoreCase`, no reflection, no file loading.
- The check is ordinal-ignore-case on the whole name; a quoted result keeps the name's original case. The existing rules (non plain, PostgreSQL capitals) are unchanged.
- Remove the `shortcut:` comment in `PostgresPrivilegeScript`; no other caller changes.

## Out of scope
- `SqlQuoting` and the DDL generators, the SQL formatter, identifiers inside user-typed text.

## Tasks
- [x] Tests first in `tests/Squill.Sql.Tests` (next to the existing `IdentifierQuoting` tests, find them with grep): per dialect, `order`, `Select`, `group`, `user`, `table` are quoted with that dialect's quote characters; `orders`, `customer_id` stay bare; PostgreSQL `user` gives `"user"`; MySQL gives `` `order` ``; SQL Server gives `[order]`. Run them red.
- [x] Implement the keyword check and make them green; fix any existing test that expected a bare keyword-like name (read each failure: a test that encoded the bug is changed, a failure that shows a real regression stops the card).
- [x] `PostgresPrivilegeScript` test: a role `user` and a table `order` produce `GRANT SELECT ON public."order" TO "user";`.
- [x] A completion test: after `select * from ` a table named `order` is inserted quoted for the three dialects; an `ON` condition on a column named `group` is quoted.

## Acceptance criteria
- Common acceptance (recipe 4), no head touched, no new package.
- Completion performance guards (the 5,000 tables test of P14-08) still pass: the keyword check is a hash lookup, not a scan.

## Risks
- Largest blast radius of the phase: every completion insertion goes through this method. Run the whole `Squill.Sql.Tests` project before and after and diff the failures.
- A keyword set that is incomplete leaves the bug for that word; pick the most complete set that exists and name its source in the Done notes.

## Suggested commit message
Quote names that are keywords of the dialect in completion and GRANT scripts

## Done notes
- Keyword source: the existing per-dialect sets in `src/Squill.Sql/Keywords` (the same ones as `AliasGenerator`); `USER` was missing from `AnsiKeywords` (an SQL-92 reserved word) and was added, so the four TextMate grammars in `assets/grammars` were regenerated with `tools/Squill.GrammarGen`.
- Tests: `IdentifierQuotingTests` (new), a `PostgresPrivilegeScript` case, a table completion case per dialect, an `ON` column case. No existing test needed changing.
