# P18-23 - Presentation: import column mapping and value conversion

Status: todo
Depends on: P18-22

## Goal
Step 3 of M10 as pure logic: map CSV columns to table columns, convert text to typed values with the options of the mockup, and flag what will fail. Recipe 1.

## Design decisions (final)
- `src/Squill.Presentation/Import/ImportMapping.cs`: `ImportMapping(IReadOnlyList<ColumnMap> Columns)`; `ColumnMap(int SourceIndex, string SourceName, ColumnDefinition? Target /* null = ignore */, bool Enabled)`. `ImportMapper.AutoMap(csvColumns, tableColumns)` matches by name (case-insensitive, ignoring `_` and `-`), then by position for the rest only when counts are equal, else leaves unmapped (the mockup maps `id_commande -> id` by the user, not automatically; do not guess synonyms).
- `ImportConversion` options (`ImportFormat`: `DecimalSeparator` `,` / `.`, `ThousandsSeparator`, `DateFormat` (`dd/MM/yyyy HH:mm`, `yyyy-MM-dd`, ISO default), `TimeZoneId` (IANA name resolved with `TimeZoneInfo.FindSystemTimeZoneById`; on failure the option is rejected), `TrueValues` / `FalseValues`, `EmptyAsNull` default true, `NullToken` optional). `ValueConverter.Convert(string? text, ColumnDefinition target, ImportFormat format)` returns `ConversionResult(object? Value, ConversionIssue? Issue)` by the CLR types the providers already accept for each column category (the parser of grid edits P8-05 knows the mapping column type -> CLR value: **reuse it** and only add the import-specific separator, date format, time zone and boolean words around it; read it first). Text columns pass through unchanged. `jsonb` / JSON columns: validity checked with `JsonDocument.Parse` (allowed under AOT), invalid -> issue `NotJson` (no silent wrapping into `{"texte": ...}`: the mockup's `{"texte": ...}` conversion is not built).
- `ImportValidator.Validate(mapping, sampleRows, format)` over the preview sample (1,000 rows) returns per-column `ColumnReport(int Checked, int Failed, string? FirstExample, ColumnIssueKind)` and a table-level list: required target column unmapped (NOT NULL without default and not identity), the same target mapped twice, a type that cannot be converted at all. `CHECK` constraint violations are **not** predicted (the schema model has no CHECK info; the mockup's `37 valeurs hors CHECK` is not built).
- Row result for the runner: `ImportRow Convert(string?[] fields, long lineNumber)` -> typed `object?[]` or `RowRejection(lineNumber, columnName, issue, rawText)`.
- Issue kinds are enum + `UiText` message keys (`import.issue.*`, fr + en) added here.

## Out of scope
- Writing to the database (P18-24), value maps, conflict handling, UI.

## Tasks
- [ ] Mapper, format options, converter reusing the P8-05 parser, validator, row conversion, catalog keys.
- [ ] Tests in `tests/Squill.Presentation.Tests/Import/`: auto-map by name and by position, French decimal `1 298,00`, date `03/01/2026 09:14` with `Europe/Paris` to the right instant (DST-safe test dates), boolean words, empty to null, invalid number reports first example, invalid JSON, NOT NULL unmapped, duplicate target, unknown time zone rejected, per-column counts over a sample.

## Acceptance criteria
- Common acceptance (recipe 7), no head touched.

## Suggested commit message
Add the import column mapping and value conversion
