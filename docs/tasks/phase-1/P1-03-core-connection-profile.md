# P1-03 - Core: dialects and connection profile model

Status: done
Depends on: P1-01

## Goal
Define the persisted description of a database connection, with no secret inside it.

## Design decisions (final)
- Namespace `Squill.Core`, project `src/Squill.Core`. New test project `tests/Squill.Core.Tests` (see README conventions), both added to `Squill.slnx`.
- `enum DatabaseDialect { PostgreSql, MySql, SqlServer, Sqlite }` (MariaDB is `MySql`).
- `sealed record ConnectionProfile(Guid Id, string Name, DatabaseDialect Dialect, string? Host, int? Port, string? Database, string? UserName, string? FilePath, SshTunnelSettings? Ssh)`; `FilePath` is used by SQLite only (`":memory:"` allowed).
- `sealed record SshTunnelSettings(string Host, int Port, string UserName, SshAuthMethod AuthMethod, string? PrivateKeyPath)`; `enum SshAuthMethod { Password, PrivateKey }`.
- Passwords and passphrases are never part of the profile. Secret store keys are built by `SecretKeys.ProfilePassword(Guid id)` => `"profile:{id:N}:password"` and `SecretKeys.SshSecret(Guid id)` => `"profile:{id:N}:ssh"` (static class in `Squill.Core`).
- `public partial class ProfileJsonContext : JsonSerializerContext` annotated `[JsonSerializable(typeof(ConnectionProfile))]` and `[JsonSerializable(typeof(List<ConnectionProfile>))]`, with `WriteIndented = true`, `PropertyNamingPolicy = CamelCase`, enums as strings (`UseStringEnumConverter = true`).

## Out of scope
- Persistence (P2-04).
- Validation rules (P4-03).

## Tasks
- [x] Create the types and `ProfileJsonContext`.
- [x] Create `tests/Squill.Core.Tests` and add it to the solution.
- [x] Tests: JSON round trip of a profile with SSH settings; serialized text contains no `password` property; `SecretKeys` format.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add Core connection profile model and JSON context
