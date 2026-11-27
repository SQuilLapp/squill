# P21-30 - Presentation: script run report, error position and stop / continue policy

Status: todo
Depends on: none

## Goal
Running a whole script (`Ctrl+Shift+Enter`) gives one `Script` result that lists every statement with its status, rows, time and error, marks the failing statement's position in the editor, and follows a stop / continue / ask policy, as DataGrip and DBeaver do.

Exists today: `RunAllCommand` (`RunTarget.RunAll`) runs the statements in order through `RunStatementsAsync`; each statement adds its own result (grid or message), the run **stops at the first failure** (`query.failedAfter`), there is no summary, no line/column of the error, no continue option. `ImportErrorPolicy` (`Stop`, `Reject`) is the precedent for a policy enum.

## Design decisions (final)
- `ScriptRunPolicy` enum: `Stop` (default, today's behaviour), `Continue`, `Ask` (on each failure: `Continue`, `Stop`, `Continue and don't ask again`); chosen per tab from the run button menu, in memory, default from a new setting `ScriptErrorPolicy` in `AppSettings`.
- `RunTarget` keeps each statement's start offset; the result `ScriptReportViewModel` (new `ResultKind.Script`) has one row per statement: index, line, first line of text, status (`ok`, `failed`, `skipped`, `cancelled`), rows affected or returned, duration, error message; clicking a row selects the statement in the editor (`ISqlEditor.Selection`) and shows its own grid/message result if any.
- Error position: the provider message often holds a position (PostgreSQL `Position`, SQL Server line number in the batch, MySQL `at line N`, SQLite `near "x"`): a pure `ErrorPositionParser` per dialect maps it to an offset inside the statement, else the statement start; shown as an error diagnostic (`SetDiagnostics` merge) until the next edit.
- Grid results of a script beyond the first 20 are not kept (only their row counts) to bound memory; the report says so.
- In a manual transaction, a failure under `Continue` on PostgreSQL puts the transaction in `Failed`: the next statements are reported `skipped` (phase 15 rules unchanged).

## Out of scope
- Heads (P21-31, P21-32), running a script file from disk without opening it (P21-49 restore uses this report).

## Tasks
- [ ] Tests: each policy, `Ask` answers, positions per dialect (golden error texts from the Docker-gated suites), 20-grid cap, PostgreSQL failed transaction skip, history entries unchanged.

## Acceptance criteria
- Recipe 2 common acceptance.

## Risks
- Statement splitting differences (`GO`, PL/SQL `/`): offsets must come from the splitter, never recomputed.

## Open points (defaults)
- Default policy `Stop` (today's behaviour).

## Suggested commit message
Add a script run report with error positions and a stop or continue policy
