# P14-19 - Documentation: completion ADR 0011, README and architecture note

Status: todo
Depends on: P14-18

## Goal
Record what phase 14 decided and what it left out, and update the user-facing docs.

## Design decisions (final)
- `docs/adr/0011-sql-completion.md` (format of [ADR 0010](../../adr/0010-execution-plan.md)): context (the audit of the recipe section 1), decisions (join rows above the plain table list; relation kinds and ranking; **side order existing-first for every join kind and why `=` makes the join kind irrelevant to the order**; which kinds get an `ON`; alias policy; quoting through `IdentifierQuoting`; schema-qualified insertion; one undo step because the editor pushes undo stops around an accepted item and the text is one plain insertion; condition not duplicated when `ON` exists; auto-open after a space; qualified ambiguous columns; ranking groups; documentation text; keyword case and functions in the session composite; snippets), alternatives considered (a snippet with placeholders to choose the foreign key: rejected, one row per key is faster to pick; kind-dependent side order: rejected, no semantic effect; item commands to chain a second popup: not supported by LoomEditor 0.5.0; generating the condition as a second step after Tab: rejected), consequences, **known limits** (no `customer_id` -> `customers.id` naming heuristic, the same-name fallback is conservative; usage ranking looks at the current statement only; the settings change reaches the popup after up to 5 seconds; the condition cannot be disabled; Oracle goes through the same code and is not verified live; popup detail strings are not translated; the plain-text fallback editor shows no documentation; `USING` is not generated; derived tables and CTEs have no relations).
- `README.md` feature list and `docs/architecture.md` (new section "SQL completion (phase 14)" in the style of the phase 13 one: the types in `Squill.Sql/Completion`, `SessionCompletionProvider`, `LoomSqlProviders` mapping), `docs/adr/0002-editor-and-terminal-components.md` point 3 gets one line pointing to ADR 0011, `docs/tasks/README.md` phase 14 status line, `docs/poc/editor.md` only if it states the completion limits (grep `Completion`).

## Out of scope
- New features.

## Tasks
- [ ] ADR, README, architecture, ADR 0002 pointer, task index status.

## Acceptance criteria
- Build and test green (docs only, but run them); links in the touched files resolve (check with a grep for the new file names).

## Suggested commit message
Document the completion improvements (ADR 0011)
