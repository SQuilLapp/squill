# P21-39 - Presentation: copy rows as INSERT / UPDATE / DELETE / CSV / JSON / Markdown, and script a table as SELECT / INSERT / UPDATE / DELETE

Status: todo
Depends on: none

## Goal
DataGrip's "Copy as" extractors and DBeaver's "Generate SQL": the selected grid rows become statements or text in another format on the clipboard, and an explorer table can be scripted as a statement template.

Exists today: grid copy as tab-separated text (`GridSelection.ToTsv`), file export as CSV and JSON (`CsvResultExporter`, `JsonResultExporter`, `IResultExporter`), `DmlGenerator` (INSERT/UPDATE/DELETE text with quoting per dialect, used by grid editing), the explorer's `Script as CREATE TABLE` (`explorer.scriptCreateTable`) and `Open definition`.

## Design decisions (final)
- `RowTextFormats` (Presentation/Results): `Tsv` (existing), `Csv`, `Json` (array of objects), `Markdown` (pipe table, `|` escaped), `Insert`, `Update`, `Delete`, `SqlInList` (one column -> `(v1, v2, ...)`). Each formats the selected rows (all columns of the selection's rows, or the selected columns only - same rule as `ToTsv`).
- SQL formats need a target table: the editable source of the result (`EditableSourceAnalyzer`) when known; otherwise `Insert` uses a placeholder name `table_name` and `Update`/`Delete` are disabled (no key known). Literals are written with `DmlGenerator`'s literal rules per dialect (dates, binary as hex, `NULL`).
- Cap: 10,000 rows per copy (a notice beyond).
- `ResultViewModel.CopyAsCommand(format)`.
- Explorer: `Script as > SELECT | INSERT | UPDATE | DELETE` next to `CREATE TABLE`: templates with every column, `:column` parameters for values, the primary key in `WHERE`; opened in a new query tab (as `Script as CREATE TABLE` does).

## Out of scope
- Head menus (P21-40), user-defined extractors (DataGrip's Groovy scripts are not reproduced).

## Tasks
- [ ] Golden tests per format and dialect (quoting, NULL, dates, binary, Markdown escaping), cap, disabled rules; explorer template tests.

## Acceptance criteria
- Recipe 2 common acceptance.

## Risks
- Large selections on the UI thread: format off the UI thread above 1,000 rows (P20-33 pattern).

## Open points (defaults)
- None.

## Suggested commit message
Copy grid rows as SQL, CSV, JSON or Markdown and script tables as DML templates
