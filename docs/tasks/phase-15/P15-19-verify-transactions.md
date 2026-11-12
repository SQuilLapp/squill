# P15-19 - End-to-end verification: transactions and production confirmation on the four engines

Status: automated done, manual pending
Depends on: P15-07, P15-09, P15-11, P15-13, P15-14, P15-16, P15-18

## Goal
Prove the whole path (query tab -> session -> transaction -> provider -> workspace) on the four engines through the real Presentation classes, and give the owner a checklist for what nobody can automate (the windows). Procedure and report format: [../phase-11/verification-recipe.md](../phase-11/verification-recipe.md) sections 1 to 3; the report is `docs/verification/transactions.md` (the owner ticks it, like [verification/plan.md](../../verification/plan.md)).

## Design decisions (final)
- Automated, `tests/Shared/TransactionFlowScenario.cs` (pattern of `PlanFlowScenario` / `QueryFlowScenario`), per engine, through `QueryTabViewModel`, `SessionManager`, the real provider and `WorkspaceViewModel` with a scripted fake `IDialogService`: (1) `Manual` mode, `INSERT` then `Valider` -> a second connection sees the row, pending count 1 then 0, `COMMIT` in history; (2) same with `Annuler` -> row gone; (3) `SELECT` in `Manual` mode opens nothing (`State` stays `None`); (4) a script of three statements where the second fails: PostgreSQL `Failed` and the third refused, others `Open` with count 1, `Annuler` clears; (5) typed `BEGIN` in `AutoCommit` mode then `INSERT` then typed `ROLLBACK`; (6) MySQL: `CREATE TABLE` inside `Open` asks (declined: nothing sent, table absent; accepted: earlier insert committed, state `None`); SQL Server / PostgreSQL / SQLite: the same DDL runs in the transaction and `Annuler` removes the table; (7) a statement that cannot run in a transaction is refused (PostgreSQL `VACUUM`, SQL Server `CREATE DATABASE`, SQLite `VACUUM`), nothing sent; (8) grid edit commit inside an open transaction then `Annuler` removes both (extends P15-09's proof through the tab); designer apply the same; (9) `Analyser` inside an open transaction leaves it open (PostgreSQL, SQL Server, MySQL); (10) Production profile: `DELETE FROM t` without `WHERE` asks with the database name required and a figure (not SQLite), declined sends nothing and the table is unchanged, accepted with the option switches to `Manual`, `Valider` on Production asks again; Staging profile never asks; (11) closing the last tab / `Déconnecter` / `PrepareExitAsync` with an open transaction asks, `Annuler` rolls back, `Retour` keeps everything, closing the session rolls back (second connection sees nothing); (12) two tabs on one session share the transaction (tab B's statement is rolled back by tab A's `Annuler`). SQLite runs always; the others Docker-gated (`rtk pwsh eng/run-docker-tests.ps1 -Engine all`).
- Manual checklist `docs/verification/transactions.md`, one section per engine (`SQLite`, `PostgreSQL`, `MySQL`, `SQL Server`), each step with the exact clicks, both heads (WinUI first, Avalonia smoke), `SQUILL_LANGUAGE=fr` and once `en`, light and dark, boxes unchecked and a blank `Result` column: toggle to `Manuel`, run an `INSERT`, pill and status text, `Annuler`, again and `Valider` with `Ctrl+Alt+C` / `Ctrl+Alt+Z` (and the AZERTY check of risk 10); a `SELECT` leaves nothing open; PostgreSQL error then `Transaction en erreur`; MySQL DDL warning; production dialog compared with M15 (header, title, statement box, typed field, checkbox, red button disabled until the word matches, `Entrée` cancels), figure present on PostgreSQL / MySQL / SQL Server and absent on SQLite; `Valider` on production asks; grid commit and designer apply on production ask; close tab, disconnect, switch to auto-commit and quit the application with an open transaction; narrow window; status bar duration updating after a minute; a Staging connection never asks. It also lists the known limits of the ADR so the owner is not surprised.
- Bugs found: fix in this commit when under ~30 lines with a regression test, else a new card `P15-21` onwards (added to the index); a known engine gap is listed in the report, not hidden.

## Out of scope
- Docs and ADR (P15-20).

## Tasks
- [x] Scenario and one test class per driver test project, `docs/verification/transactions.md`, clean-up (`podman ps -a` empty, `dotnet build-server shutdown`).

## Acceptance criteria
- Common acceptance of the recipe, section 9, Docker run for all engines green with nothing skipped, no container left. The card status is `automated done, manual pending` until the owner ticks the checklist.

## Suggested commit message
Verify transactions and the production confirmation end to end and add the manual checklist
