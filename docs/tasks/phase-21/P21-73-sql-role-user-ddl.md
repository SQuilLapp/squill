# P21-73 - Sql: create, alter and drop users and roles, and role membership, per dialect

Status: todo
Depends on: none

## Goal
The statements behind a fuller user and role management screen (DBeaver and DataGrip let you create, alter and drop users and roles).

Exists today: `Squill.Core/Security` (`RoleInfo` with memberships, `ObjectPrivileges`, `PrivilegeChangeSet`), `PostgresPrivilegeScript` (GRANT / REVOKE on tables, P18-18); the security tab is PostgreSQL only and read/grant only (ADR 0015 "Not built: role creation, default privileges").

## Design decisions (final)
- `RoleChange` model (Core): `Create`, `Alter`, `Drop`, `AddMember`, `RemoveMember`, with fields `Name`, `CanLogin`, `Password` (never logged, sent as a literal only in the generated statement shown masked in the preview), `Superuser`, `CreateDb`, `CreateRole`, `ValidUntil`, `ConnectionLimit` (PostgreSQL); host part for MySQL (`'user'@'host'`); login vs user for SQL Server (`CREATE LOGIN` server level + `CREATE USER ... FOR LOGIN` in the database, `ALTER ROLE ... ADD MEMBER`).
- `RoleScript.Build(dialect, changes) -> MigrationScript` for PostgreSQL, MySQL 8 (`CREATE USER`, `CREATE ROLE`, `GRANT role TO user`, `ALTER USER ... IDENTIFIED BY`), SQL Server; SQLite and Oracle: not supported (empty with a reason).
- Preview text masks the password (`'********'`); the executed text has it; history stores the masked text (check how history records statements; if it stores executed text, the runner gets a `MaskedText` for history).

## Out of scope
- Readers (P21-74), UI (P21-75 ...), default privileges, row-level security.

## Tasks
- [ ] Golden tests per dialect and change; masking test (preview and history).

## Acceptance criteria
- Recipe 2 common acceptance.

## Risks
- A password literal reaching the history file or a log: test that it does not.

## Open points (defaults)
- None.

## Suggested commit message
Generate user and role DDL for PostgreSQL, MySQL and SQL Server
