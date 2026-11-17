# P18-17 - Data.Postgres: read roles and privileges

Status: done
Depends on: P18-16

## Goal
Read the data of M11 from a PostgreSQL server, read-only. Recipe 1, 4.

## Design decisions (final)
- Contract `src/Squill.Data/ISecurityReader.cs` (optional capability like `IPlanReader`; `null` on every other engine): `ValueTask<IReadOnlyList<RoleInfo>> ReadRolesAsync(CT)`, `ValueTask<IReadOnlyList<ObjectPrivileges>> ReadPrivilegesAsync(string role, IReadOnlyList<string> schemas, CT)`, `ValueTask<IReadOnlyList<string>> ListSchemasAsync(CT)`.
- `PostgresSecurityReader`: roles from `pg_roles` (`rolcanlogin`, `rolsuper`, `rolinherit`) excluding `pg_*` predefined roles by default (parameter `includeSystem`), memberships from `pg_auth_members`; object privileges from `pg_class` (`relkind` r, p, v, m) joined to `aclexplode(coalesce(relacl, acldefault('r', relowner)))`, per requested role: **direct** = grantee is the role, **inherited** = grantee is a role the subject is a (transitive, `rolinherit`) member of, with the nearest source role (`pg_has_role(role, grantee, 'USAGE')`). The owner of a table has all privileges implicitly through `acldefault`: shown as `Granted`. `PUBLIC` grants (grantee 0) count as inherited from `PUBLIC`. Only the 7 privilege kinds of M11 (`MAINTAIN` and `SET` of newer versions are ignored). Schemas excluded: `pg_catalog`, `information_schema`, `pg_toast`. One query per call, no per-object round trips (5,000 tables must stay under a second).
- All reads need no special right (catalog views are public); an unreadable ACL is not an error.

## Out of scope
- Writing (statements run through the statement runner, P18-19), other engines.

## Tasks
- [x] Contract, reader, provider hook.
- [x] Docker-gated `tests/Squill.Data.Postgres.Tests/SecurityReaderTests.cs`: create roles `grp_read` (nologin) granted `select` on a schema's tables, `analyst` (nologin, member of `grp_read`), `reader` (login), grant `insert` on one table directly to `analyst`: roles list and group / user split, memberships both ways, `analyst` shows `Inherited(grp_read)` for select and `Granted` for the direct insert, the owner shows everything granted, `PUBLIC` grant shows inherited from `PUBLIC`, schema filter, views and materialized views present, a 2,000-table schema reads quickly.

## Acceptance criteria
- Common acceptance (recipe 7); the Docker-gated test ran on PostgreSQL (say so).

## Suggested commit message
Add the PostgreSQL roles and privileges reader
