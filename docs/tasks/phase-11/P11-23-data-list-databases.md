# P11-23 - Data: list the databases of a server

Status: done
Depends on: P11-02, P11-03, P11-04

## Goal
A connection can list the other databases of its server (PostgreSQL, MySQL, SQL Server) so that the explorer can offer them; today `IDatabaseConnection` reads only the database of the profile (`database()`, `current_database()`, the `InitialCatalog`).

## Design decisions (final)
- `IDatabaseConnection` gets `ValueTask<IReadOnlyList<string>> ListDatabasesAsync(CancellationToken ct)` (XML doc; SQLite returns a single entry, the file name, because one file is one database). Update `FakeDatabaseProvider` and every implementer.
- Queries: PostgreSQL `select datname from pg_database where not datistemplate and datallowconn order by 1`; MySQL `select schema_name from information_schema.schemata where schema_name not in ('information_schema','mysql','performance_schema','sys') order by 1`; SQL Server `select name from sys.databases where state = 0 and name not in ('master','tempdb','model','msdb') order by 1` (databases the login cannot access are listed anyway; opening them fails with the driver message, shown by P11-24).
- A profile with an empty database is allowed for the three engines (connect to the server default: `postgres`, none, `master`); check that `BuildConnectionString` and the profile validation accept it and add a test.
- Docker-gated tests per engine: create a second database, list, assert both appear and the system ones do not.

## Out of scope
- Using the list (P11-24), creating or dropping databases.

## Tasks
- [x] Contract, four implementations, tests.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` 0 warnings; default test run green; `rtk pwsh eng/run-docker-tests.ps1 -Engine all` green; no container left.

## Suggested commit message
List the databases of a server through the connection
