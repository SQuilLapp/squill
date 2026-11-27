# P21-41 - Presentation: follow a foreign key from a grid cell, and back to the referencing rows

Status: todo
Depends on: none

## Goal
From a cell of a foreign key column, open the referenced row; from a row of a referenced table, list and open the rows that reference it (DataGrip "Go to referenced / referencing data", DBeaver "References" panel).

Exists today: `SchemaSnapshot` with foreign keys (`ForeignKeyInfo`, composite keys); `EditableSourceAnalyzer` knows the single source table of a plain `SELECT`; query tabs run SQL. No navigation from a cell.

## Design decisions (final)
- Available only when the result's source table is known (`EditableSourceAnalyzer` result, even when the result is not editable for another reason such as a missing key column) and the FK columns are all in the result.
- `ResultViewModel.ForeignKeysAt(column)` -> the FKs whose columns include it; `OpenReferencedRowCommand(row, fk)` opens a new query tab (same connection) with `SELECT * FROM <parent> WHERE <pk cols> = :p1 [AND ...]`, the values bound as parameters (no literal in the text, no prompt: values pre-filled and run directly).
- `ReferencingTables(row)` -> the FKs of other tables pointing to the source table; `OpenReferencingRowsCommand(row, fk)` runs `SELECT * FROM <child> WHERE <fk cols> = ...` the same way. A `NULL` key disables the command.
- Quoting through `SqlQuoting`; schema-qualified names where needed (P14-03 rule).

## Out of scope
- Heads (P21-42), an inline references panel under the grid.

## Tasks
- [ ] Tests on the SQLite scenario: single and composite FK, NULL key disabled, unknown source disabled, referencing list content, generated SQL per dialect.

## Acceptance criteria
- Recipe 2 common acceptance.

## Risks
- A result with a renamed column (`SELECT a AS b`) must not match the FK: match on the source column name from the analyzer, not the header.

## Open points (defaults)
- Open in a new tab (default) rather than replacing the current result.

## Suggested commit message
Follow foreign keys from a grid cell to the referenced and referencing rows
