# P3-01 - Sql: token model and generic tokenizer

Status: todo
Depends on: P1-04

## Goal
Lossless, never-throwing SQL tokenizer base, plus the ANSI keyword set.

## Design decisions (final)
- Project `src/Squill.Sql` (references `Squill.Core`), namespace `Squill.Sql.Lexing`; tests in `tests/Squill.Sql.Tests` (`InternalsVisibleTo` from `Squill.Sql` to the test assembly).
- `enum TokenKind { Whitespace, LineComment, BlockComment, Keyword, Identifier, QuotedIdentifier, StringLiteral, NumberLiteral, Operator, Punctuation, Parameter, Unknown, EndOfFile }`; `readonly record struct Token(TokenKind Kind, int Start, int Length)`; `Token.Text(string source)` helper.
- `interface ISqlTokenizer { IReadOnlyList<Token> Tokenize(string text); }` (no `EndOfFile` token is emitted; consumers handle the end) and `abstract class SqlTokenizerBase : ISqlTokenizer` with virtual hooks `TryReadSpecial(string text, int pos, out Token token)`, `Keywords` (`FrozenSet<string>`, `StringComparer.OrdinalIgnoreCase`) and `IsIdentifierStart/Part`.
- `AnsiTokenizer : SqlTokenizerBase` handles: whitespace runs, `--` line comments, `/* */` block comments (not nested; unterminated runs to the end), `'...'` strings with `''` escape (unterminated runs to the end of the text), `"..."` quoted identifiers with `""` escape, numbers (`12`, `1.5`, `.5`, `1e10`), identifiers/keywords, operators (`+ - * / % = < > <= >= <> != ||`; `::` is Postgres only), punctuation `( ) , ; .`.
- Invariants: tokens are contiguous and cover the whole text (sum of lengths == text length); any other character becomes `Unknown` of length 1. `Squill.Sql` public surface so far: `ISqlTokenizer`, `Token`, `TokenKind`, `SqlTokenizers.For(DatabaseDialect)` (returns `AnsiTokenizer` for every dialect for now).
- `AnsiKeywords` in `Keywords/AnsiKeywords.cs`: the ~150 reserved words of SQL-92/99 core (SELECT, FROM, WHERE, JOIN, ... ) as a `FrozenSet<string>`.

## Out of scope
- Dialect specifics (P3-02, P3-03).
- Parsing.

## Tasks
- [ ] Create the project and types; add both projects to `Squill.slnx`.
- [ ] Tests: each token kind, unterminated string/comment, contiguity invariant on 50 hand-written inputs and a seeded random-character fuzz (1,000 inputs of 0-200 chars, `new Random(42)`), keyword case-insensitivity.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add SQL token model and generic tokenizer
