# P11-24 - Presentation: browse other databases of a connection

Status: done
Depends on: P11-23

## Goal
Under a connected server node the explorer lists the server's databases (PostgreSQL, MySQL, SQL Server); expanding another one opens a session on it and shows its schemas, and a query tab can target the chosen database.

## Design decisions (final)
- Read `ExplorerTreeViewModel` and `SessionManager` first: the tree today is connection > database > schema > objects for the profile's database only (P9-26). The Database level lists all entries of `ListDatabasesAsync`; the profile's own database stays first and expanded by default.
- Expanding another database creates a derived session (`ConnectionSession` with the same profile but `Database` replaced, same password and SSH tunnel; PostgreSQL and SQL Server cannot switch database on an open connection, MySQL could with `USE` but one derived session per database keeps one code path). Derived sessions are owned by the parent session and closed with it; lazily opened, reused on a second expand, errors shown on the node (driver message, node collapses).
- A query tab opened from a node of that database uses that derived session; the tab title shows `connection / database` only when it is not the profile's own.
- `RefreshSchemaAsync` refreshes the derived session of the node.

## Out of scope
- Heads (they render the existing node kind `Database`; verify in P11-36 ... P11-39), creating databases.

## Tasks
- [x] Tree, session ownership, tab binding, tests with `FakeDatabaseProvider` (two databases, one failing to open), catalog keys.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` 0 warnings; `rtk bypass dotnet test --solution Squill.slnx -c Release` green.
- No session leak: a test disposes the parent and asserts every derived connection was disposed.

## Suggested commit message
Browse and query the other databases of a server from the explorer
