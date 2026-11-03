# P12-02 - Sql: variable scanner, masking and highlight kind

Status: todo
Depends on: P12-01

## Goal
Find `{{name}}` references in SQL text with exact offsets, outside strings and comments, and let the existing parser, diagnostics, completion and hover keep working on text that contains variables. Specification: [variables-spec.md](variables-spec.md) section 2.

## Design decisions (final)
- No change to the four tokenizers. `Squill.Sql.Variables.VariableScanner.Scan(string sql, DatabaseDialect dialect)` tokenizes with `SqlTokenizers.For(dialect)` to learn the ranges of string literals, quoted identifiers, comments and dollar-quoted bodies (read `SqlTokenizerBase` and `PostgresTokenizer` for how `$$` bodies are emitted), then scans the gaps with the pattern of spec section 2. A `{{` that is not a valid reference (bad name, no closing `}}`, newline inside the braces) is ignored.
- Result `VariableReference(TextRange Range, string Name, VariableMode Mode)`; `VariableMode` is `Bound`, `Identifier`, `Raw` (prefix none, `ident:`, `raw:`). `Range` covers the whole `{{...}}`.
- `VariableScanner.Mask(string sql, IReadOnlyList<VariableReference> refs)` returns text **of identical length**: a `Bound` reference becomes `@` followed by `_` for the rest, an `Identifier` or `Raw` one becomes `_` repeated. The masked text is what `SqlDiagnosticsProvider`, `SqlScopeResolver`, the completion and hover providers and the statement splitter receive, so offsets map 1:1. Mask once where the `SqlRequest` / analysis input is built (read those entry points first), not in every provider, and add a test per provider that a statement containing `{{x}}` produces no new diagnostic.
- `HighlightKind.Variable` is added; `SqlHighlighter.Classify` emits one span per reference range (the whole `{{...}}`) after the token spans so it wins. `SqlGrammarGenerator` and the Avalonia `SqlColorizer` ignore the new kind until P12-11 (a `default` branch keeps them compiling; a test asserts the generated grammars are unchanged).
- Pure, O(n), no reflection.

## Out of scope
- Resolution, colours, UI.

## Tasks
- [ ] `VariableScanner`, `VariableReference`, `VariableMode`, `HighlightKind.Variable`, masking wired into the analysis entry points.
- [ ] Tests (`tests/Squill.Sql.Tests/Variables/`) on all four dialects: simple, spaces inside, two on one line, `ident:` and `raw:`, inside `'...'` (not found), inside `--` and `/* */` comments (not found), inside `"..."`, `[...]` and backticks (not found), PostgreSQL `$$ {{x}} $$` (not found), `::` casts, `$1`, `@p`, `?` next to a variable (unchanged, only the variable found), invalid names, unterminated `{{a`, `{{a}` and `{{{a}}}`, multi-line offsets, mask length equals input length, diagnostics of `WHERE a = {{x}}` are empty.

## Acceptance criteria
- Build 0 warnings, tests green, existing Sql tests unchanged and green.

## Suggested commit message
Scan query variables in SQL text and mask them for analysis
