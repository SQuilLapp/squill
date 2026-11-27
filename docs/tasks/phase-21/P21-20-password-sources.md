# P21-20 - Platform and Presentation: password sources (stored, ask on connect, external command)

Status: done
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
- [x] Tests: `Ask` cancelled aborts the connect; `Command` success, timeout, non-zero exit, empty output; confirmation on changed command; nothing written to the secret store for `Ask` and `Command`.

## Acceptance criteria
- Recipe 2 common acceptance.

## Risks
- A command that waits for input blocks: stdin is closed, timeout kills it.

## Open points (defaults)
- None.

## Suggested commit message
Let a connection ask for its password or read it from an external command

## Done notes
- Premise checked: `ISecretStore`, `SecretKeys`, `SessionManager` exist as stated; no password source, no `PromptPasswordAsync` existed.
- Added `PasswordSource` (`Stored` default, `Ask`, `Command`) and the last optional members `ConnectionProfile.PasswordSource` / `PasswordCommand` (older `profiles.json` load unchanged; the command line is in the profile, it is not a secret). `IDialogService.PromptPasswordAsync(profileName)` is a default interface member that cancels. `SessionManager` takes optional `dialogs` and `commandRunner` and resolves the password before anything else: `Ask` keeps it in memory for the session (derived database sessions reuse it, nothing is written), cancelling the prompt or declining the confirmation is an `OperationCanceledException` (no error banner).
- `Squill.Platform/Secrets/PasswordCommandRunner`: command line split by a small tokenizer (white space separates, double quotes group, backslash literal) into `ProcessStartInfo.ArgumentList`, `UseShellExecute=false`, no shell, standard input closed, 10 s timeout that kills the process tree, output capped at 64 K chars, neutral working directory (Windows searches the current directory before PATH), stdout trimmed, empty output is an error. Failures are a `PasswordCommandException` (kind, exit code, first 500 chars of stderr as the card asks); stdout is never put in an exception, a message or a log (tested). The program is found as the operating system finds it (PATH for a bare name); nothing is installed or written.
- Confirmation: `IProfileStore.IsCommandTrustedAsync` / `TrustCommandAsync` (default interface members) keep a SHA-256 of the confirmed command line in the secret store (`SecretKeys.PasswordCommandTrust`, deleted with the profile), never in the profile file. A new, changed or imported profile therefore asks once, showing the command, before it runs; this also covers a profile imported from a file, since trust is not part of the file. Limit: SSH secrets still use the stored source only (out of scope).
- Tests: 11 in `PasswordCommandTests` (tokenizer, trimmed output, metacharacters not interpreted, non-zero exit without stdout leak, empty output, timeout, closed stdin, trust store), 9 in `PasswordSourceTests` (ask, cancelled ask, confirmation then remembered, changed command asks again, declined runs nothing, failures, stored default, nothing written to the store). The existing `Serialize_Profile_ContainsNoPassword` test forbade the word "password" in the JSON, so it now removes the two new setting names first.
- Not run: AOT publish (no head code changed; `Process` with an argument list is AOT-safe), Docker suites (not needed by the card).
