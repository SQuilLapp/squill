# P3-15 - Sql: highlighter and TextMate grammars

Status: todo
Depends on: P3-14

## Goal
Syntax highlighting for the AvaloniaEdit interim and SQL TextMate grammars for LoomEditor (ADR 0002, point 2).

## Design decisions (final)
- `HighlightKind { Keyword, Identifier, QuotedIdentifier, StringLiteral, Number, Comment, Operator, Parameter, Function }`; `HighlightSpan(int Start, int Length, HighlightKind Kind)`; `SqlHighlighter.Classify(string text, DatabaseDialect d)` from the tokenizer (whitespace and punctuation omitted; an identifier directly followed by `(` and present in `FunctionCatalog` is `Function`).
- `SqlGrammarGenerator.Generate(DatabaseDialect d)` (public static, in `Squill.Sql`) returns the JSON text of a TextMate grammar: `scopeName` `source.sql.postgresql|mysql|tsql|sqlite`, patterns for line/block comments, strings (with the dialect's escape/quote forms), quoted identifiers, numbers, parameters, functions, and one keyword alternation built from the dialect keyword set (`keyword.control.sql`), case-insensitive.
- `tools/Squill.GrammarGen` (Exe, AOT like `Squill.TokenGen`) writes `assets/grammars/sql-<dialect>.tmLanguage.json` for the four dialects; the generated files are committed.
- A unit test regenerates each grammar in memory and asserts it equals the committed file (found by walking up from the test output directory to the repo root) so keyword sets and grammars cannot drift.

## Out of scope
- Registering grammars in LoomEditor (P5-13).

## Tasks
- [ ] Implement highlighter and generator; create the tool and add it to the solution.
- [ ] Run the tool and commit the four grammar files.
- [ ] Tests for classification per dialect and for the grammar/file equality.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).
- `rtk dotnet publish tools/Squill.GrammarGen -c Release -r win-x64` succeeds with zero IL2xxx/IL3xxx warnings.
- `rtk dotnet run --project tools/Squill.GrammarGen -- assets/grammars` leaves `git status` clean.

## Suggested commit message
Add SQL highlighter and TextMate grammar generator
