# P21-70 - Presentation: cross-engine mode of the schema compare tab (pair picker, mapping review)

Status: todo
Depends on: P21-69

## Goal
Point 6 of the index analysis: the compare tab accepts a SQLite source and a PostgreSQL target, shows a mapping review step, then the comparison and the script.

Exists today: `SchemaCompareViewModel` (same dialect; pickers refuse different dialects), its script pane, `ExecuteScriptCommand` through the runner and the production confirmation.

## Design decisions (final)
- The dialect check allows exactly the pair SQLite -> PostgreSQL (others keep the same-dialect rule with a message listing the supported pair).
- A `MappingReview` step before the result: one row per source column whose mapping has a `Loss` or is not exact (source type, proposed target type, loss text, `Accept` check, override combo of target types); `Compare` is enabled when every lossy row is accepted or overridden.
- The result list uses the existing rows with the new verdict `Equivalent` shown muted; the script pane shows P21-69's script; run and save as today.

## Out of scope
- Heads (P21-71, P21-72), other pairs.

## Tasks
- [ ] Tests: pair allowed, other pairs refused with the message, review gating, override changes the script, production confirmation on run.

## Acceptance criteria
- Recipe 2 common acceptance.

## Risks
- Must not change the same-dialect behaviour: the existing compare tests stay unchanged and green.

## Open points (defaults)
- None.

## Suggested commit message
Add the SQLite to PostgreSQL mode with a mapping review to the schema compare tab
