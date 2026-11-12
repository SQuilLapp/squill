# P15-20 - Documentation: transactions ADR 0012, README and architecture note

Status: todo
Depends on: P15-19

## Goal
Record what phase 15 decided and what it left out, and update the user-facing docs.

## Design decisions (final)
- `docs/adr/0012-transactions-and-production-confirmation.md` (format of ADR 0010): context, decisions (transaction owned by the session, why not per tab; modes and lazy begin; state machine and the PostgreSQL `Failed` state; probe as an optional interface; journal and pending count; savepoint scopes for the grid commit and the designer; implicit-commit confirmation and refused statements; close / disconnect / exit questions; production confirmation rules, typed database name only for destructive statements, estimate rules and the 3 s cap, `Entrée` = `Annuler`; `Analyser` keeps its plain confirmation), the per-engine matrix of the recipe, consequences, **known limits** (tabs of one session share the transaction, two tabs running at once on one connection, MySQL non-transactional engines and implicit commits, `SET autocommit` / `IMPLICIT_TRANSACTIONS` only picked up by the probe, connection lost mid-transaction is not reconnected, no idle timeout, the MySQL probe result found by P15-06, estimates are planner figures, shortcuts that clash on some layouts, quit paths that cannot be intercepted), what is not done (per-tab connections, default mode per profile or environment, `Staging` confirmation, isolation level, savepoint UI, long-transaction warning, Oracle, MariaDB).
- `docs/design/visual-spec.md`: section 10 loses "M15 production confirmation dialog" and "auto-commit/manual transaction controls of the toolbar"; add a short section "Screen M15 and the transaction controls" (what is drawn, from recipe sections 2 and 3, and what is not: the schema picker, the pinned tab) and a status row in section 12 with what the capture reports of P15-15 ... P15-18 said was compared and not compared.
- `README.md` (feature list: transactions and production confirmation; support matrix per engine: manual transactions, implicit-commit warning, estimate availability), `docs/architecture.md` (`Squill.Core/Transactions`, `Squill.Sql/Transactions`, `ITransactionProbe`, `SessionTransaction`, `Squill.Presentation/Safety`, the two new `IDialogService` members), `docs/tasks/README.md` phase 15 status line and rows, `docs/design/mockups/README.md` if it tracks coverage (grep `M15`), `docs/verification/transactions.md` linked from the README.

## Out of scope
- New features.

## Tasks
- [ ] ADR, spec, README, architecture, task index status.

## Acceptance criteria
- Build and test green (docs only, but run them); links in the touched files resolve (check with a grep for the new file names).

## Suggested commit message
Document transactions and the production confirmation (ADR 0012)
