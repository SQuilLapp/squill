# P12-13 - Tests: localization and secret-leak guards for variables

Status: todo
Depends on: P12-10, P12-12

## Goal
Make the two cross-cutting promises of phase 12 enforced by tests: every new string exists in French and English and is used, and a secret variable value never leaves the secret store except into the bound parameter of the statement.

## Design decisions (final)
- Localization: the existing guards of P10-17 (parity, used keys, string scanner over code and XAML) must pass with all `variables.*` keys; fix any hard-coded string they report in the new views, never extend the ignore list for them. Add a test that lists the `variables.*` keys and asserts French and English values differ (except identical technical words) and plural keys have `.one` and `.other`.
- Secret leaks: one test class in `tests/Squill.Presentation.Tests/Variables/` that runs a query tab with a fake connection and a secret variable `{{token}}` = a unique marker string, then asserts the marker is absent from: the history entry, `StatusText`, every result message (including a failing statement), `VariableChipsViewModel` text, `VariablePopupModel` text, `ResolvedVariable.ToString()`, the manager rows (masked) and the serialized `variables.json`; and present only in the `QueryParameter` sent to the connection. Another test that `ident`/`raw` refuse a secret.
- Allowed leak documented in the ADR: a database error message that echoes a bound value.

## Out of scope
- New features.

## Tasks
- [ ] Tests and any string fixes they reveal; update the phase 10 inventory only if a guard requires it.

## Acceptance criteria
- Build 0 warnings; full `rtk bypass dotnet test --solution Squill.slnx -c Release` green; no weakened or deleted guard.

## Suggested commit message
Guard variable strings and secret values with tests
