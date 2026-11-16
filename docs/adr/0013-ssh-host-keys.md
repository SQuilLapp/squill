# ADR 0013 - SSH host keys

- Status: accepted
- Date: 2026-11-16

## Context
Until phase 16 `SshNetTunnelFactory` trusted every host key the jump host presented (a `shortcut:` comment marked it). A man in the middle on the way to the jump host was invisible, and the database password travels through that tunnel. Cards P16-04 to P16-07 ([task index](../tasks/README.md)) replace it with a check against a store of known hosts.

## Decision
- **Trust on first use, like OpenSSH.** The first key a host presents is shown to the user and, if accepted, remembered. Later connections compare the presented key with the remembered one.
- **Store.** `IKnownHostsStore` (`Squill.Platform/Ssh`) with `JsonKnownHostsStore` (`known-hosts.json` in the app data directory; `Location` gives the path) and an in-memory store for tests. One entry per `host:port` (lower-cased) and key algorithm. A corrupted file is treated as empty.
- **Fingerprint.** `SHA256:` followed by the base64 of the SHA-256 of the key blob without padding, the format OpenSSH and administrators use.
- **Rules**, decided in `SshNetTunnelFactory` (`DecideAsync`): known and equal, trust; known and different, refuse with `SshHostKeyChangedException` (carries the stored and presented fingerprints and the file path), never ask and never replace the stored key; unknown with a prompt, ask, a yes saves the key and trusts, a no refuses with `SshHostKeyRejectedException`; unknown and nobody to ask (`prompt` is null), refuse the same way. There is no silent trust.
- **Errors in the app.** A declined key is a cancelled connection attempt (no error banner). A changed key is a connection error (`ssh.hostkey.changed`) that shows both fingerprints and the file path, and says to remove the entry only if the server was legitimately reinstalled.
- **The question** is `IDialogService.ConfirmSshHostKeyAsync` (a `ContentDialog` in WinUI, a modal window in Avalonia). It shows the host, port, algorithm and the fingerprint in a selectable monospace box. `Annuler` is the default button: `Entrée`, `Echap` and closing the window cancel, only a click on `Faire confiance` trusts.

## Consequences
- A reinstalled or replaced server is refused until the user edits `known-hosts.json`; the error names the file.
- The unit tests drive the four rules through `DecideAsync`; the Docker-gated jump host tests prove the first open prompts once and saves, the second does not prompt, and a wrong stored fingerprint refuses.

## Known limits
- No "forget host key" command: the user edits the file named in the error.
- The user's OpenSSH `known_hosts` is not read.
- Host certificates are not supported.

## Not done
- A "forget host key" button in the connection editor, a host key algorithm preference, importing the OpenSSH file.
