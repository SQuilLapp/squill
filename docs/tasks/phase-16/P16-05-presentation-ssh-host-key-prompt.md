# P16-05 - Presentation: the host key question and the host key errors

Status: todo
Depends on: P16-04

## Goal
A first connection through an SSH tunnel asks the user to trust the host key (showing its fingerprint); a changed key is refused with a clear message; both heads get the dialog contract.

## Design decisions (final)
- `IDialogService` gets `ValueTask<bool> ConfirmSshHostKeyAsync(SshHostKeyRequest request)` (XML doc: true = trust and remember). Same call from any thread as the other members. Every implementation and fake is updated: both heads get a **temporary** implementation that returns `false` (declines, so the build and tests stay green and nothing is trusted silently), replaced by P16-06 (WinUI) and P16-07 (Avalonia); `tests/Shared` fakes record the request and return a scripted answer.
- `SessionManager` receives the prompt through its constructor as `SshHostKeyPrompt` (the composition root `AppComposition` builds it from `IDialogService.ConfirmSshHostKeyAsync`; tests pass their own). `OpenAsync` passes it to `tunnels.OpenAsync`.
- Errors: `SshHostKeyRejectedException` becomes a cancelled connection attempt (status "connection cancelled", no error banner, nothing opened, like the user cancelling), `SshHostKeyChangedException` becomes the normal connection error path with message code `ssh.hostkey.changed` and arguments `[host, port, storedFingerprint?, presentedFingerprint, locationOfKnownHostsFile]`; texts in both catalogs (fr and en, parity tests green): the changed-key text states that the server identity differs from the remembered one, that the connection was refused, shows both fingerprints, and tells the user to remove the entry for `host:port` from the known-hosts file (path given) only if the server was legitimately reinstalled. The stored fingerprint must be reachable from the exception (extend `SshHostKeyChangedException` in P16-04's type with `StoredFingerprint`, a one-line change allowed here).
- The dialog title and body strings for the first-use question go through `UiText` too: `ssh.hostkey.title`, `ssh.hostkey.body` (host, port, algorithm), `ssh.hostkey.trust`, `ssh.hostkey.cancel`, used by the heads in P16-06 / P16-07.
- A profile without SSH is unaffected; the Docker-gated `SshSessionTests` scenario keeps passing with an accepting prompt.

## Out of scope
- The dialogs themselves (P16-06, P16-07), a "forget host key" button in the connection editor.

## Tasks
- [ ] `IDialogService` member, temporary head implementations, fakes, `AppComposition` and `SessionManager` wiring.
- [ ] Catalog keys (fr, en) and the error mapping.
- [ ] Tests (`tests/Squill.Presentation.Tests/Sessions/SshSessionTests.cs` pattern, fake tunnel factory that raises the three outcomes): first use accepted opens the session, declined leaves no session and no error banner, changed key shows the localized message with both fingerprints and the file path in French and English, a non-SSH profile never calls the prompt.

## Acceptance criteria
- Common acceptance, both solutions build (`Squill.Windows.slnx` for WinUI), localization guards green.

## Suggested commit message
Ask before trusting a new SSH host key and refuse a changed one
