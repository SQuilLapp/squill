# P13-22 - Documentation: execution plan ADR 0010, README and architecture note

Status: todo
Depends on: P13-21

## Goal
Record what phase 13 decided and what it left out, and update the user-facing docs.

## Design decisions (final)
- `docs/adr/0010-execution-plan.md` (format of ADR 0009): context, decisions (own plan tab, `Expliquer` never executes and `Analyser` executes read-only statements only, per-engine mechanisms and wrappers, `IPlanReader` as an optional interface, parsers pure and AOT-safe, relative cost computed in Core, per-engine data availability), consequences, **known limits** (parallel plans approximate, 8 kB block size assumption, MySQL `EXPLAIN ANALYZE` text parsing depends on the 8.x format, MariaDB unsupported, parameters limits found by the tests, MySQL has no analyze wrapper, SQL Server showplan state risk and its mitigation), what is not done (compare, plan history, index suggestions for PostgreSQL/MySQL, plan export, Oracle).
- `docs/design/visual-spec.md`: section 10 loses "M06 plan"; add a short section 13 "Screen M06 - Execution plan" with what is drawn and what is not (copy section 1 and 2 of [plan-recipe.md](plan-recipe.md) in condensed form), and a status row in section 12 with what the capture reports of P13-15 ... P13-20 said was compared and not compared.
- `README.md` (feature list, support matrix by engine: Expliquer / Analyser per engine), `docs/architecture.md` (new `Squill.Core/Plans`, `IPlanReader`, `Squill.Presentation/Plans`, the plan tab), `docs/tasks/README.md` phase 13 status line, mockups README if it tracks coverage (grep `M06`).

## Out of scope
- New features.

## Tasks
- [ ] ADR, spec, README, architecture, task index status.

## Acceptance criteria
- Build and test green (docs only, but run them); links in the touched files resolve (check with a grep for the new file names).

## Suggested commit message
Document the execution plan feature (ADR 0010)
