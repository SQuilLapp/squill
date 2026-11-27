# P21-101 - Oracle: an empty password gives "Value cannot be null"

Status: todo
Depends on: P21-03

## Goal
Found by the P21-09 drive: a profile saved without a stored password (written by hand into `profiles.json`) connects with the dialog "Connection failed: Value cannot be null." because `OracleDatabaseProvider.OpenAsync` sends `Password: null` to ODP.NET. Other engines give a readable answer.

## Design decisions (final)
- Send an empty string instead of null and let the server answer (`ORA-01017`), or refuse early with the existing "wrong credentials" wording; pick the first unless the server message is unreadable.

## Out of scope
- Prompting for the password (a separate feature).

## Tasks
- [ ] Unit test through the fake bridge (`open` request carries an empty, not null, password) and a Docker test for the message.

## Acceptance criteria
- Recipe 2.

## Suggested commit message
Send an empty Oracle password instead of null
