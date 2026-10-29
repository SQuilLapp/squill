# P2-04 - Platform: app paths and JSON profile store

Status: todo
Depends on: P2-03

## Goal
Persist connection profiles with their secrets in the secret store.

## Design decisions (final)
- `AppPaths` (`Squill.Platform`): `DataDirectory` = `%APPDATA%\SQuilL` on Windows, `~/Library/Application Support/SQuilL` on macOS, `$XDG_CONFIG_HOME/squill` (fallback `~/.config/squill`) on Linux; constructor `AppPaths(string? overrideDirectory = null)` for tests; `Combine(params string[])` creates the directory.
- `interface IProfileStore { ValueTask<IReadOnlyList<ConnectionProfile>> ListAsync(CancellationToken); ValueTask SaveAsync(ConnectionProfile profile, string? password, string? sshSecret, CancellationToken); ValueTask DeleteAsync(Guid id, CancellationToken); ValueTask<string?> GetPasswordAsync(Guid id, CancellationToken); ValueTask<string?> GetSshSecretAsync(Guid id, CancellationToken); }` and `JsonProfileStore(AppPaths, ISecretStore)`.
- File `profiles.json` serialized with `ProfileJsonContext`. Writes are atomic (write `profiles.json.tmp`, then `File.Move(..., overwrite: true)`); a corrupt file is renamed `profiles.json.bad` and treated as empty. Saving with `password == null` keeps the existing secret; empty string deletes it. Deleting a profile deletes both secrets.

## Out of scope
- Import/export of profiles.
- Encryption of the JSON file (it contains no secrets).

## Tasks
- [ ] Implement `AppPaths`, `IProfileStore`, `JsonProfileStore`.
- [ ] Tests with a temp directory and `InMemorySecretStore`: round trip, update, delete removes secrets, corrupt file recovery, `profiles.json` text contains no password.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add app paths and JSON profile store
