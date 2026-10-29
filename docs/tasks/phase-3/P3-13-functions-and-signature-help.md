# P3-13 - Sql: function catalog, function completion and signature help

Status: todo
Depends on: P3-12

## Goal
Built-in function metadata per dialect with completion and signature help.

## Design decisions (final)
- `FunctionSignature(string Name, IReadOnlyList<string> Parameters, string Returns, string Description)`; `FunctionCatalog.For(DatabaseDialect)` returns a static list: common (COUNT, SUM, AVG, MIN, MAX, COALESCE, NULLIF, CAST, LENGTH/LEN, SUBSTRING, UPPER, LOWER, TRIM, REPLACE, ABS, ROUND, CURRENT_DATE...) plus ~10 dialect-specific ones each (e.g. `date_trunc`, `string_agg` for Postgres; `DATE_FORMAT`, `IFNULL` for MySQL; `GETDATE`, `ISNULL`, `DATEADD` for T-SQL; `strftime`, `ifnull` for SQLite).
- `FunctionCompletionProvider : ISqlCompletionProvider` (Kind `Function`, insert text is the bare name, detail = signature) active in expression contexts (select list, WHERE, ON, HAVING, ORDER BY, SET).
- `SignatureHelpProvider.GetSignatureHelp(SqlRequest)` returns `SignatureHelp(FunctionSignature Signature, int ActiveParameter)?`: finds the innermost unclosed `(` preceded by an identifier present in the catalog (case-insensitive) using the tokenizer, counts top-level commas between it and the offset (nested parentheses ignored).

## Out of scope
- User-defined functions from the database.

## Tasks
- [ ] Create the catalog files and providers.
- [ ] Tests: each provider, nested calls, strings with commas, unknown function => null.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add function catalog, completion and signature help
