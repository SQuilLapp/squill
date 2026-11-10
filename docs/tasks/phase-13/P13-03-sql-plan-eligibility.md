# P13-03 - Sql: which statements can be explained or analyzed

Status: todo
Depends on: P13-01

## Goal
One place decides, per dialect, whether a statement may be explained (`Expliquer`) and whether it may be analyzed (`Analyser`, which executes it), with a reason the UI can show.

## Design decisions (final)
- `Squill.Sql/Plans/PlanEligibility.Evaluate(string statement, DatabaseDialect dialect)` returns `PlanEligibility(bool CanExplain, bool CanAnalyze, string? ExplainReasonCode, string? AnalyzeReasonCode)` (record, codes from `PlanMessageCodes`, added by P13-02: `plan.reason.empty`, `plan.reason.alreadyexplain`, `plan.reason.notexplainable`, `plan.reason.writes`, `plan.reason.noanalyze`).
- Uses the existing tokenizers of the dialect (never regex over raw text): first significant keyword decides.
  - Explainable: `SELECT`, `WITH`, `VALUES`, `TABLE` (PostgreSQL / MySQL), `INSERT`, `UPDATE`, `DELETE`, `MERGE` (PostgreSQL, SQL Server; MySQL `INSERT/UPDATE/DELETE/REPLACE`), `EXEC` is **not** (SQL Server showplan of a procedure call is not a plan of its statements): reason `notexplainable`. DDL, `PRAGMA`, `SET`, `BEGIN`, ... not explainable. A statement already starting with `EXPLAIN` (or SQL Server `SET SHOWPLAN`) is refused with `alreadyexplain`. Empty or comment-only: `empty`.
  - Analyzable: explainable **and read-only**: the first keyword is `SELECT`/`VALUES`/`TABLE`, or `WITH` whose token stream contains none of `INSERT`, `UPDATE`, `DELETE`, `MERGE`, `INTO` (the `SELECT ... INTO` of SQL Server and PostgreSQL creates a table), `FOR UPDATE`/`FOR SHARE` is allowed. Otherwise `writes`. SQLite: `CanAnalyze` always false with `noanalyze` (the engine has no actual plan).
- Takes one statement (the caller passes what `RunTarget` resolved); if the tokenizer sees more than one statement it evaluates the first and the caller never passes more.

## Out of scope
- Rewriting a statement, running anything.

## Tasks
- [ ] `PlanEligibility` and tests per dialect (`tests/Squill.Sql.Tests/Plans/PlanEligibilityTests.cs`): select, cte-select, cte-with-delete (explain yes, analyze no), `select ... into`, insert/update/delete, `exec`, ddl, `explain select`, empty, comment only, lowercase, leading comments, SQLite analyze always false.

## Acceptance criteria
- Common acceptance of the recipe, section 5.

## Suggested commit message
Decide which statements can be explained or analyzed
