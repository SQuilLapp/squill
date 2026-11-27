# P21-20 - Platform and Presentation: password sources (stored, ask on connect, external command)

Status: todo
Depends on: none

## Goal
Besides the OS secret store, let a profile ask for the password at each connect, or read it from an external secret tool (HashiCorp Vault CLI, 1Password `op`, `pass`, Azure `az`), as DBeaver's secret providers and DataGrip's "never store" do.

Exists today: `ISecretStore` with DPAPI (Windows), libsecret (Linux) and macOS Keychain stores (P2-01 ... P2-03), `SecretKeys.ProfilePassword` / `SshSecret`; `SessionManager` reads the stored password and opens the connection; there is no "ask every time" and no external source. The OS keychain part of the owner's list is **already done**.

## Design decisions (final)
- Profile field `PasswordSource`: `Stored` (default, today's behaviour), `Ask` (prompt at connect, kept in memory for the session only, never written), `Command` (a command line run without a shell, stdout trimmed is the password, 10 s timeout, stderr shown on failure).
- `IDialogService.PromptPasswordAsync(profileName)` (heads implement it in P21-21; a default returning `null` cancels).
- Command safety: the command line is stored in the profile (not a secret), run with `ProcessStartInfo.ArgumentList` (no shell), its output never logged; the first run of a new or changed command asks for confirmation showing the command.
- Vault over HTTP is **not** built: the `vault kv get -field=password ...` command covers it without a token in SQuilL.

## Out of scope
- UI (P21-21). SSH secrets use the same sources in a later card if wanted.

## Tasks
- [ ] Tests: `Ask` cancelled aborts the connect; `Command` success, timeout, non-zero exit, empty output; confirmation on changed command; nothing written to the secret store for `Ask` and `Command`.

## Acceptance criteria
- Recipe 2 common acceptance.

## Risks
- A command that waits for input blocks: stdin is closed, timeout kills it.

## Open points (defaults)
- None.

## Suggested commit message
Let a connection ask for its password or read it from an external command
