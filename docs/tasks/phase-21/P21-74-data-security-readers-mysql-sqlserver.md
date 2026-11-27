# P21-74 - Data: security readers for MySQL and SQL Server (roles, users, memberships)

Status: todo
Depends on: P21-73

## Goal
`ISecurityReader` on MySQL and SQL Server, so the security tab is no longer PostgreSQL only.

Exists today: `ISecurityReader` (`ReadRolesAsync`, `ReadPrivilegesAsync`, `ListSchemasAsync`), implemented by PostgreSQL only ("every engine but PostgreSQL for now"); `RoleInfo`.

## Design decisions (final)
- MySQL: `mysql.user` (users and roles: `account_locked`, `is_role` via `mysql.role_edges`), memberships from `mysql.role_edges`; privileges on tables from `information_schema.TABLE_PRIVILEGES` and `SCHEMA_PRIVILEGES` mapped to `ObjectPrivileges`; a user without `SELECT` on `mysql.*` gets `ActivityUnavailableException`-like `SecurityUnavailableException` with the engine message.
- SQL Server: database principals `sys.database_principals` (users, roles), `sys.database_role_members`, logins from `sys.server_principals` when visible; privileges from `sys.database_permissions` on tables and views.
- `RoleInfo` gets the optional fields P21-73 needs (`CanLogin`, `Host` for MySQL) without breaking PostgreSQL.
- `PrivilegeScript` for MySQL and SQL Server GRANT/REVOKE (the PostgreSQL one exists).

## Out of scope
- UI (P21-75).

## Tasks
- [ ] Docker-gated tests on MySQL and SQL Server: users, roles, membership, table privileges, GRANT/REVOKE round trip.

## Acceptance criteria
- Recipe 2 common acceptance; Docker-gated tests green.

## Risks
- MySQL global privileges (`*.*`) do not fit the table matrix: shown as a note, not as table cells.

## Open points (defaults)
- None.

## Suggested commit message
Read roles, users and privileges on MySQL and SQL Server
