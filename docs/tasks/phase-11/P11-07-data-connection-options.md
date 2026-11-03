# P11-07 - Data: apply the connection options in the four providers

Status: todo
Depends on: P11-06, P11-04

## Goal
Each provider turns `TlsMode`, `TrustServerCertificate` and the timeouts into its own connection string, and `CommandTimeoutSeconds` bounds every statement.

## Design decisions (final)
- Mapping (all in `BuildConnectionString`, testable without a server):
  - PostgreSQL (`NpgsqlConnectionStringBuilder`): `Disable` -> `SslMode.Disable`, `Require` -> `Require` (or `Prefer` when `Default`; keep the Npgsql default for `Default`), `VerifyFull` -> `VerifyFull`; `Timeout` = connect timeout; `CommandTimeout` = command timeout (0 = Npgsql 0 = infinite).
  - MySQL (`MySqlConnectionStringBuilder`): `SslMode` `None`/`Required`/`VerifyFull` accordingly, `ConnectionTimeout`, `DefaultCommandTimeout` (0 = infinite).
  - SQL Server: `Encrypt` (`Optional`/`Mandatory`/`Strict` is NOT used; use `Optional` for `Disable`, `Mandatory` for `Require` and `VerifyFull`), `TrustServerCertificate` = profile flag **or** host is `localhost`/`127.0.0.1`/`::1` (keeps today's behaviour for loopback, removes the literal-name trap found in P11-04), `ConnectTimeout`, `Command Timeout` through `SqlCommand.CommandTimeout` (set in `ExecuteAsync`).
  - SQLite: `CommandTimeoutSeconds` only (`SqliteCommand.CommandTimeout`); the other options are ignored.
- The command timeout is applied in each `ExecuteAsync` (`command.CommandTimeout`), not only in the connection string, so that the provider tests can set it per connection.
- A statement that exceeds the timeout throws the driver exception unchanged; the message shown to the user is handled by the query tab (P11-10 asserts it).

## Out of scope
- UI, SSH.

## Tasks
- [ ] Implement the mapping in the four providers; unit tests per provider that parse the produced string back with the builder (no server).
- [ ] Docker-gated tests (run with P11-01 script): TLS `Require` connects on each server (PostgreSQL and MySQL images support TLS with their self-signed certificate: `Require` must work, `VerifyFull` must fail with a certificate error, and the test asserts the failure); SQL Server connects with `TrustServerCertificate=true` through host `127.0.0.1`.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` 0 warnings; default test run green.
- `rtk pwsh eng/run-docker-tests.ps1 -Engine all` green; no container left.

## Suggested commit message
Map the TLS and timeout options to each driver
