# P3-14 - Sql: formatter

Status: done
Depends on: P3-13, P2-07

## Goal
Deterministic, idempotent SQL formatting.

## Design decisions (final)
- `SqlFormatter.Format(string text, DatabaseDialect d, FormatOptions options)`; `FormatOptions(KeywordCase KeywordCase, int IndentSize)` with `Default` = (`Upper`, 4); `KeywordCase` is the Core enum from P2-07.
- Token-based (no AST): each statement is formatted separately; newline + base indent before `SELECT`, `FROM`, `WHERE`, `GROUP BY`, `HAVING`, `ORDER BY`, `LIMIT`, `UNION [ALL]`, `INTERSECT`, `EXCEPT`, `VALUES`, `SET`, `RETURNING`, and before each join keyword group (`LEFT OUTER JOIN` is one unit); `ON` goes on a new line indented once; select items one per line indented once; content of parentheses that contains `SELECT` is indented one level deeper than its opening line; other tokens separated by a single space with no space before `,` `)` `;` `.` or after `(` `.`.
- Keyword case applies to `Keyword` tokens only; identifiers, quoted identifiers, strings, numbers, parameters and comments are preserved byte for byte; line comments force a newline after them; text of unparseable statements is returned unchanged.
- Idempotent: `Format(Format(x)) == Format(x)`. Statements are separated by one blank line; the original terminating `;` is kept.

## Out of scope
- Range formatting, line width wrapping, per-clause options.

## Tasks
- [x] Implement the formatter.
- [x] Tests: golden inputs/outputs for select/join/subquery/cte/insert/update, comments, strings with keywords inside, case options, idempotency on all golden outputs, each dialect's quoting.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add SQL formatter
