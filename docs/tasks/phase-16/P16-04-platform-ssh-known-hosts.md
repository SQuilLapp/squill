# P16-04 - Platform: SSH host keys are checked and remembered

Status: todo
Depends on: none

## Goal
`SshNetTunnelFactory` accepts every host key (`HostKeyReceived ... e.CanTrust = true`, with a `shortcut:` comment): a man in the middle on the way to the jump host is invisible and the database password travels through it. Make the factory check the key against a store of known hosts (trust on first use, like OpenSSH). Procedure for engine proof: [verification-recipe.md](../phase-11/verification-recipe.md).

## Design decisions (final)
- New in `src/Squill.Platform/Ssh/`: `public sealed record SshHostKeyRequest(string Host, int Port, string Algorithm, string Fingerprint)` (`Fingerprint` = `SHA256:` + base64 without padding of the SHA-256 of the key blob, the OpenSSH format; from the `FingerPrintSHA256` of SSH.NET's `HostKeyEventArgs`, read the package, never invent members), `public delegate ValueTask<bool> SshHostKeyPrompt(SshHostKeyRequest request, CancellationToken ct)`, `public interface IKnownHostsStore { ValueTask<string?> FindAsync(string host, int port, string algorithm, CancellationToken ct); ValueTask SaveAsync(SshHostKeyRequest key, CancellationToken ct); string Location { get; } }`, `JsonKnownHostsStore` (file `known-hosts.json` under `AppPaths`, `JsonFileStore` pattern of `JsonTableFilterStore`, source-generated context entry, key `host:port` lower-cased, one entry per algorithm) and an in-memory store for tests.
- `ISshTunnelFactory.OpenAsync` gains a last parameter `SshHostKeyPrompt? prompt` (before `ct`). `SshNetTunnelFactory(IKnownHostsStore store)`. Rules, evaluated in `HostKeyReceived` (it is synchronous: block on the async calls with `GetAwaiter().GetResult()` inside it, it runs on the connect thread, not the UI thread; comment why): known and equal -> trust; known and different -> `CanTrust = false`, remember the reason, and `OpenAsync` throws `SshHostKeyChangedException(request, store.Location)` (never prompts, never replaces the stored key); unknown and a prompt exists -> ask, `true` saves the key and trusts, `false` refuses and `OpenAsync` throws `SshHostKeyRejectedException(request)`; unknown and `prompt` is null -> refuse with `SshHostKeyRejectedException` (secure by default, no silent trust). Both exceptions derive from a new `SshHostKeyException : Exception` carrying the request. SSH.NET reports a refused key as a connection exception: map it to the right exception by the reason remembered in the handler.
- Remove the `shortcut:` comment. Update every caller: `SessionManager` (passes `null` for now; P16-05 wires the prompt), `SshJumpHostTests` and `SshNetTunnelFactoryTests` (Docker-gated, they pass a prompt that accepts and then prove the second open does not prompt), `ScenarioStubs` and the other fakes of `ISshTunnelFactory`.

## Out of scope
- Any UI (P16-05 ... P16-07), a "forget host key" command, reading the user's OpenSSH `known_hosts`, host key algorithms preference, certificates.

## Tasks
- [ ] Types, store, factory change, exceptions, caller updates.
- [ ] Tests (`tests/Squill.Platform.Tests/`): store round trip and a corrupted file (treated as empty, not a crash), fingerprint format, `JsonKnownHostsStore` file location, the four decision rules through a fake key event if SSH.NET allows one, else through a small `internal` decision method `Decide(known, presented, prompt)` that the handler calls (tested table-driven); Docker-gated jump host test: first open prompts once and saves, second open does not prompt, a stored wrong fingerprint throws `SshHostKeyChangedException` and the tunnel is not open (`rtk pwsh eng/run-docker-tests.ps1 -Engine ssh`, nothing skipped, no container left).
- [ ] AOT publish of `tools/Squill.AotProbe.SshNet` with zero IL2xxx/IL3xxx warnings.

## Acceptance criteria
- Common acceptance, Docker run for the SSH tests green, AOT probe publishes.

## Suggested commit message
Check SSH host keys against a known-hosts store instead of trusting every key
