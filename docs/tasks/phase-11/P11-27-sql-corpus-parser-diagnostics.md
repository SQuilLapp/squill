# P11-27 - Dialect corpus: parser and diagnostics against real servers

Status: done
Depends on: P11-02, P11-03, P11-04

## Goal
For each dialect, a corpus of valid statements that real servers accept must produce zero diagnostics, split correctly, and tokenize without errors; and a corpus of invalid statements must be reported. The hand-written parser (ADR 0003) was tested on crafted samples, never on statements validated by an engine.

## Design decisions (final)
- Corpus files `tests/Shared/SqlCorpus/<dialect>.valid.sql` and `<dialect>.invalid.sql`, statements separated by lines `-- ---`, each valid statement self-contained against the sample schema of its engine. About 60 valid statements per dialect covering: joins of every kind, subqueries and CTEs (recursive), window functions, set operations, `INSERT ... SELECT`, `UPDATE ... FROM|JOIN`, `DELETE ... USING|JOIN`, DDL, and dialect specifics: PostgreSQL `::` casts, `$$` and `$tag$` bodies, `RETURNING`, `ON CONFLICT`, `LATERAL`, `generate_series`, `array[...]`, `jsonb ->>`, `FILTER (WHERE ...)`; MySQL backticks, `LIMIT x, y`, `ON DUPLICATE KEY UPDATE`, `DELIMITER` scripts, `#` and `-- ` comments; SQL Server `TOP`, `OFFSET FETCH`, `[brackets]`, `OUTPUT`, `MERGE`, `CROSS APPLY`, `GO` batches, `#temp` tables, `N'unicode'` strings; SQLite `pragma`, `without rowid`, `INSERT OR REPLACE`, `||`, `glob`, `strftime`. Each invalid statement is a one-error statement with the expected diagnostic message code.
- Test A (no container, runs everywhere, in `Squill.Sql.Tests`): every valid statement: `SqlStatementSplitter` yields exactly the statement, `SqlDiagnosticsProvider` returns no error-level diagnostic with the sample `SchemaSnapshot` (built by hand in the test from the sample schema description); every invalid statement yields the expected code.
- Test B (Docker-gated, in each driver test project): every valid statement is **executed** inside a rolled-back transaction (or `EXPLAIN`/`SET FMTONLY`/`SET NOEXEC ON` when it cannot run; DDL on PostgreSQL and SQL Server runs in a transaction, MySQL DDL runs on a scratch database dropped afterwards) and must succeed, so a corpus statement the server rejects is a corpus bug. SQLite executes against the sample file.
- Failures of test A are parser/diagnostic bugs: fix in `Squill.Sql` with focused unit tests; do not drop corpus lines. A construct that cannot be supported in this card is marked `-- unsupported: <reason>` in the corpus, skipped by test A with the reason printed, and listed in the report.

## Out of scope
- Completion, hover, formatting (P11-28).

## Tasks
- [x] Corpus files (valid and invalid), tests A and B, parser fixes.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` 0 warnings; default test run green; `rtk pwsh eng/run-docker-tests.ps1 -Engine all` green; no container left.
- The report lists every `unsupported` marker.

## Suggested commit message
Validate the parser and diagnostics against statements real servers accept
