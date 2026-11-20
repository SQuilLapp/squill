# P20-21 - Docs: ADR 0017, changelog, the multi-delete checklist for the owner, index

Status: done
Depends on: P20-01 ... P20-20

## Goal
Record what phase 20 changed and what is still open, and give the owner the checklist of the new feature (prepared, never ticked by an agent).

## Design decisions (final)
- `docs/adr/0017-verification-and-multi-delete.md` (format of ADR 0016: Status accepted, date, Context, Decision, Consequences, Known limits, Not built): the window driver and its foreground rule (P20-01), the WinUI theme lookup guard (P20-02), the PostgreSQL scenario scripts (P20-03), the multi-delete decisions of recipe 4 (tables only, one session, no `CASCADE`, order by foreign keys, blockers stop the drop, atomic on PostgreSQL / SQL Server / SQLite and not on MySQL / Oracle, the single drop now goes through the runner and the production confirmation: say that this closes a gap where a Production `DROP TABLE` ran without confirmation), what each verification card left `not driven` (copy from their Done notes), Linux / macOS never run.
- `docs/verification/multi-delete.md` (same format as `docs/verification/transactions.md`: Status line, Automated (done) with the test counts of P20-04 ... P20-09, How to prepare (scenario `-Scenario Full`, `tools/visual/New-PostgresScenario.ps1`), a Checklist table with WinUI / Avalonia tick columns and a `Result` column, Known limits). Rows: select two tables with Ctrl+click and Shift+click; context menu text `Supprimer 2 tables`; `Delete` key; a view in the selection disables it; tables of two connections disable it; confirmation lists the tables; cancel does nothing; drop order with foreign keys (`commandes` before `clients`); a table outside the selection that references one blocks with the list; Production: production dialog with the typed database name and statements; open manual transaction on PostgreSQL: the drops are journaled and `Annuler` brings the tables back; MySQL: implicit commit question and the partial message after a failure; SQLite file; designer tab of a dropped table closes; explorer reloads; light / dark / French / English; both heads. Prefilled `Result` cells only for rows P20-07 / P20-08 drove (`agent: ok`), the boxes stay empty.
- `CHANGELOG.md` under `## [Unreleased]` (format rules at the top of the file; no tables, images or nested lists): Added (drop several tables at once from the explorer), Fixed (dropping a table on a Production connection now asks the production confirmation and respects the open transaction; one bullet per fix the verification cards made, from their Done notes).
- `docs/ideas.md`: remove the multi-table delete line (done) and keep the cross-engine compare line, pointing at the README "what it would need" list.
- `docs/tasks/README.md`: set every card status to the file's, add `P20-22` onwards for the cards the verification passes proposed (index row and card file), rewrite the phase header status line. `docs/design/mockups/README.md` "In the app today" rows only if a pass changed them. Checklist `Status:` lines only (what the agent passes did), no tick, no step text edit. Check every relative link added.

## Out of scope
- Any code.

## Tasks
- [x] ADR, checklist, changelog, ideas, README statuses, spec rows; link check by opening each new target path.

## Acceptance criteria
- Docs only: `rtk git status` shows only documentation files and `CHANGELOG.md`; the index has one row per card file in `docs/tasks/phase-20/`; statuses match the files.

## Suggested commit message
Document phase 20: verification passes and multi-table delete
