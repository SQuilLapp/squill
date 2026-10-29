# P3-03 - Sql: MySQL and T-SQL tokenizers

Status: todo
Depends on: P3-02

## Goal
Dialect-specific lexing for MySQL/MariaDB and SQL Server.

## Design decisions (final)
- `MySqlTokenizer`: backtick identifiers, `#` line comments, `--` comments only when followed by whitespace or end of text, `"` delimits a string literal (ANSI_QUOTES off), backslash escapes inside strings, parameters `?`, `@name`, system variables `@@name` (`Parameter` for `@name`/`?`, `Identifier` for `@@name`), `/*! ... */` treated as a block comment; keywords in `Keywords/MySqlKeywords.cs`.
- `TSqlTokenizer`: `[ident]` (with `]]` escape) as `QuotedIdentifier`, `N'...'` strings, `@name` as `Parameter`, `#temp`/`##temp` as `Identifier`, nested block comments; keywords (`TOP`, `NOLOCK`, `IDENTITY`, `OUTPUT`, `MERGE`, ...) in `Keywords/TSqlKeywords.cs`. `GO` is a plain identifier here (handled by the splitter).
- `SqlTokenizers.For` returns these for `MySql` and `SqlServer`; fuzz invariant per dialect.

## Out of scope
- Statement splitting (P3-04).

## Tasks
- [ ] Implement both tokenizers and keyword sets.
- [ ] Tests per special form + fuzz invariant.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add MySQL and T-SQL tokenizers
