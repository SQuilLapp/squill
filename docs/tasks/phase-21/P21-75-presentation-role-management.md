# P21-75 - Presentation: user and role management in the security tab

Status: todo
Depends on: P21-73, P21-74

## Goal
Create, alter and drop users and roles and change memberships from the security tab, on PostgreSQL, MySQL and SQL Server.

Exists today: `SecurityViewModel` (roles list, privilege matrix, GRANT/REVOKE script, apply through the runner with the production confirmation, rows-cut notice), PostgreSQL only.

## Design decisions (final)
- The tab opens on any connection whose session has an `ISecurityReader` (PostgreSQL, MySQL, SQL Server).
- Role list toolbar: `New user`, `New role`, `Edit`, `Drop`; an editor view-model `RoleEditorViewModel` (fields per dialect from P21-73, password twice with a match check, members and member-of lists with add/remove); every change goes into the pending script shown in the existing script pane; `Apply` through the statement runner and the production confirmation (drop is destructive: typed confirmation on Production).
- Dropping a role that owns objects: the engine error is shown; no `REASSIGN OWNED` / `DROP OWNED` generated (open point).

## Out of scope
- Heads (P21-76, P21-77).

## Tests and tasks
- [ ] Tests: dialect fields, script preview masked, apply path with production confirmation, drop refused message, membership edits.

## Acceptance criteria
- Recipe 2 common acceptance.

## Risks
- SQL Server logins are server-level: creating a login needs server rights; the error must be readable.

## Open points (defaults)
- Owner: generate `REASSIGN OWNED BY ... TO ...` on PostgreSQL drop (default no).

## Suggested commit message
Create, alter and drop users and roles from the security tab
