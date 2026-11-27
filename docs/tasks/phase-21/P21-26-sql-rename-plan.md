# P21-26 - Sql: rename plan for a table or a column in a script text

Status: todo
Depends on: P21-22

## Goal
Compute every edit needed to rename a table or a column inside one SQL text, without touching strings, comments or other objects that share the name.

Exists today: `SqlSymbolResolver` (P21-22) gives the symbol and its usages in a script; `SqlQuoting` quotes per dialect.

## Design decisions (final)
- `Squill.Sql/Refactoring/RenamePlanner` (new folder): `Plan(string text, DatabaseDialect dialect, RenameTarget target, string newName, SchemaSnapshot? schema) -> RenamePlan` with `Edits` (`TextRange`, replacement), `Skipped` (ambiguous or unresolved occurrences with a reason) and `ApplyTo(text)`.
- `RenameTarget`: a table (schema, name) or a column (schema, table, name).
- Only occurrences resolved to the target are edited (an alias with the same name is not; an unqualified column is edited only when its single visible source is the target table). Quoting is re-applied when the new name needs it; the original quoting style is kept when the new name allows it.
- A text that does not parse still gets token-level matches for qualified names (`t.col`, `schema.table`) but unqualified ones go to `Skipped`.

## Out of scope
- Where the texts come from and the preview (P21-27).

## Tasks
- [ ] Table-driven tests per dialect: alias sharing the name, column of another table with the same name, string and comment occurrences untouched, quoted mixed case, `JOIN ... USING (col)`, CTE column lists.

## Acceptance criteria
- Recipe 2 common acceptance.

## Risks
- A wrong edit silently corrupts a script: when in doubt, skip and report, never edit.

## Open points (defaults)
- None.

## Suggested commit message
Plan the edits that rename a table or a column in a SQL text
