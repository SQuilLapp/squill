# P16-02 - Sql: formatter select modifiers and glued custom delimiters

Status: done
Depends on: none

## Goal
Close two known gaps recorded as `shortcut:` comments in `Squill.Sql`: the formatter and a custom statement delimiter glued to an identifier.

## Design decisions (final)
- Formatter (`src/Squill.Sql/Formatting/SqlFormatter.cs`, `BreakBefore`, comment "only `TOP n` and `DISTINCT ON (...)` are recognised"). Write the test table first, run it, and fix only the rows that fail (the current code may already handle `TOP (n)`; if so the comment is stale and is removed). Rows, T-SQL unless said: `SELECT TOP 10 a, b FROM t`, `SELECT TOP (10) a, b FROM t`, `SELECT TOP 10 PERCENT a FROM t`, `SELECT TOP (10) PERCENT WITH TIES a FROM t ORDER BY a`, `SELECT DISTINCT TOP 5 a FROM t`, `SELECT TOP (@n) a FROM t` (variable), PostgreSQL `SELECT DISTINCT ON (a) a, b FROM t`, and `SELECT ALL a FROM t`. Expected: the modifier stays on the `SELECT` line, the item list starts on the next line exactly as for a plain `SELECT a, b`; keyword case follows the existing `KeywordCase` option. Formatting must stay idempotent (format twice = format once) for every row.
- Splitter (`src/Squill.Sql/Statements/SqlStatementSplitter.cs`, comment "a custom delimiter glued to an identifier (`END$$`) is not split"). MySQL `DELIMITER $$` scripts end a body with `END$$` with no space. Fix: when the delimiter text starts exactly at the end of an identifier or keyword token (the tokenizer swallowed it into the token), split the token at the delimiter start instead of requiring token alignment. Add cases: `DELIMITER $$` / `CREATE PROCEDURE p() BEGIN SELECT 1; END$$` / `DELIMITER ;` gives one statement for the procedure body; `END $$` (spaced) still works; a delimiter inside a string or a comment never splits; the default `;` is unaffected (including `a;b`).
- Behavior of every existing test stays; public API unchanged.

## Out of scope
- `FETCH` row count in `SelectParser`, formatter style options, other dialect modifiers.

## Tasks
- [x] Formatter table test (`tests/Squill.Sql.Tests/Formatting/`), fix the failing rows, remove the stale comment.
- [x] Splitter cases in the existing splitter test class, fix, remove the comment.

## Acceptance criteria
- Common acceptance. `SqlFormatter` and splitter tests green, idempotence test included.

## Suggested commit message
Format TOP and WITH TIES select modifiers and split a delimiter glued to END
