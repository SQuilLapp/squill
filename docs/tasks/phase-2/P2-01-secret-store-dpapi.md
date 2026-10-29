# P2-01 - Platform: secret store contract, in-memory and DPAPI stores

Status: done
Depends on: P1-03

## Goal
Introduce `Squill.Platform` with the secret storage abstraction and the Windows implementation (risk R6, part 1).

## Design decisions (final)
- Project `src/Squill.Platform` (references `Squill.Core`), namespace `Squill.Platform.Secrets`; tests in `tests/Squill.Platform.Tests`.
- `interface ISecretStore { ValueTask SetAsync(string key, string secret, CancellationToken ct); ValueTask<string?> GetAsync(string key, CancellationToken ct); ValueTask DeleteAsync(string key, CancellationToken ct); }`; deleting a missing key is a no-op.
- `InMemorySecretStore` (ConcurrentDictionary), used by tests everywhere.
- `[SupportedOSPlatform("windows")] DpapiSecretStore(string directory)`: one file per key named `<lowercase hex SHA-256 of key>.bin` containing `ProtectedData.Protect(UTF-8 secret, null, DataProtectionScope.CurrentUser)`. Package `System.Security.Cryptography.ProtectedData` (latest stable 10.0.x, pinned in `Directory.Packages.props`). Directory created on first write.

## Out of scope
- Linux/macOS stores (P2-02, P2-03).
- A factory that picks the store (P2-03).

## Tasks
- [x] Create the project, contracts and stores; add to `Squill.slnx`.
- [x] Tests: in-memory behaviours; DPAPI round trip in a temp directory (skipped when not Windows via `[Fact(SkipUnless = ...)]`), file content is not the plaintext.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add secret store contract with in-memory and DPAPI stores
