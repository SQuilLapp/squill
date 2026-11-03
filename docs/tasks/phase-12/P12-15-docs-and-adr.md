# P12-15 - Documentation: query variables ADR 0009, README and architecture note

Status: todo
Depends on: P12-14

## Goal
Record what was built and decided. Same layout as ADR 0007 (Status accepted, Date, Context, Decision, Known gaps, Follow-ups).

## Design decisions (final)
- `docs/adr/0009-query-variables.md` (0008 is the engine parity ADR of P11-40; if that number is taken differently, use the next free one) states: the `{{name}}` syntax and why it is safe (spec section 2), the three modes (bound, `ident:`, `raw:`), scopes and precedence, storage and secrets, run-time substitution and the injection tests, the relation to `:name` prompt parameters (unchanged), the editor capabilities per head and the LoomEditor limit with its fallback (spec section 7: no action inside the hover, caret-based click, chip row), where variables are not recognised, and the allowed leak (driver messages echoing a bound value).
- Known gaps from the reports of P12-08 ... P12-14. Follow-ups (proposals only): variables in connection profile fields, folder scope, import / export of variable sets and Postman / Bruno environment import, variables inside string literals with an explicit marker, a LoomEditor content widget or link handler when available, dynamic variables (`{{$now}}`), variable sets selected per tab.
- Update `README.md` (feature, syntax example, scopes), `docs/architecture.md` (Presentation `Variables` paragraph, Platform store), `docs/tasks/README.md` (phase 12 status and final decisions), `docs/tasks/phase-12/variables-spec.md` stays as the detailed reference.

## Out of scope
- Code changes.

## Tasks
- [ ] ADR, README, architecture note, statuses of every phase 12 card and index row set to `done`.

## Acceptance criteria
- `rtk bypass dotnet build Squill.slnx -c Release -warnaserror` exits 0 once; every relative link of the ADR resolves; no process left running.

## Suggested commit message
Document the query variables decisions in ADR 0009
