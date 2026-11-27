# P21-93 - AI assistant (2/5): schema context, privacy filter and the payload preview

Status: todo
Depends on: P21-92

## Goal
Build what is sent to the provider - the schema as context, never data rows by default - and an exact, readable preview the user confirms before anything leaves the machine.

Exists today: `SchemaSnapshot` (tables, columns, keys, FKs, views, routines), `SqlSymbolResolver` (P21-22) or the scope resolver to find the tables of the current statement, connection `Environment` (Production).

## Design decisions (final)
- `AssistantContextBuilder`: dialect and engine version; the tables referenced by the editor statement plus their FK neighbours, then other tables by name until `MaxSchemaTables`; for each table only `CREATE TABLE`-like text (names, types, keys) - **no** row data, no column statistics, no comments unless the user ticks `Include comments`.
- Sample rows only when `SendSampleRows` is on **and** the user ticks it for this request: at most 5 rows of the current result, each value capped at 100 characters.
- Redaction: connection names, host names, user names and file paths are never included; string literals in the user's SQL can be replaced by `'?'` (`Redact literals`, default on for Production connections, off otherwise).
- `AssistantPayloadPreview`: the exact text that will be sent (system + user messages), its size and the estimated tokens (characters / 4), the provider and base URL; the request is sent only from `ConfirmAndSend`.
- Production connections: the preview shows a warning line; nothing else differs.

## Out of scope
- The assistant view-model and actions (P21-94).

## Tests and tasks
- [ ] Tests: table selection and cap, FK neighbours, no data by default, sample rows only with both switches, redaction of literals and names, preview equals what the fake provider receives (byte for byte).

## Acceptance criteria
- Recipe 2 common acceptance.

## Risks
- A preview that differs from the real payload would break trust: the provider receives the preview object itself, not a rebuilt one.

## Open points (defaults)
- Owner: default for `Redact literals` outside Production (default off).

## Suggested commit message
Build the AI assistant schema context with a privacy filter and an exact payload preview
