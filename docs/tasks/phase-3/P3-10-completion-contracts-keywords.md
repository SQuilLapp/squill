# P3-10 - Sql: completion contracts and keyword completion

Status: todo
Depends on: P3-09

## Goal
LSP-shaped completion types and the first provider (keywords by clause).

## Design decisions (final)
- Namespace `Squill.Sql.Completion`. `enum CompletionKind` with the LSP numeric values used (Text=1, Method=2, Function=3, Field=5, Class=7, Module=9, Keyword=14, Snippet=15, Struct=22); `sealed record CompletionItem(string Label, CompletionKind Kind, string InsertText, string? Detail, string? SortText, TextRange ReplaceRange)`.
- `sealed record SqlRequest(string Text, int Offset, DatabaseDialect Dialect, SchemaSnapshot? Schema)` (shared by completion, hover, diagnostics, signature help); `interface ISqlCompletionProvider { IReadOnlyList<CompletionItem> GetCompletions(SqlRequest request); }`.
- `ReplaceRange` covers the identifier/keyword prefix being typed (from the last non-identifier boundary to `Offset`); providers return all candidates valid in context and never filter by prefix (the editor filters; `CompletionFilter.Filter(items, prefix)` in this namespace does case-insensitive prefix-then-contains filtering for the AvaloniaEdit interim).
- `KeywordCompletionProvider`: at statement start => statement keywords (SELECT, INSERT, UPDATE, DELETE, CREATE, ALTER, DROP, WITH, + dialect ones); after a complete FROM source => JOIN/WHERE/GROUP BY/ORDER BY/LIMIT...; inside SELECT list => DISTINCT, CASE, ...; chosen from `ScopeInfo.Clause`. Keyword sets come from the dialect's keyword class. Kind `Keyword`, text uppercased.

## Out of scope
- Schema-aware items (P3-11).

## Tasks
- [ ] Create the types and the provider.
- [ ] Tests: replace range, clause-dependent keyword sets, dialect-specific items (`TOP` only for T-SQL, `RETURNING` only for Postgres), filter helper.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add completion contracts and keyword completion
