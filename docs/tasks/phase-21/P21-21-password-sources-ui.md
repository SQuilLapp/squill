# P21-21 - Password sources in both heads (editor choice and the password prompt)

Status: done
Depends on: P21-20

## Goal
Choose the password source in the connection editor and answer the password prompt, in both heads.

Exists today: the password box of the connection editor; `IDialogService` implementations `WinUiDialogService` / `AvaloniaDialogService` with `PromptAsync`.

## Design decisions (final)
- Editor: a source combo above the password box (`Saved in the system keychain`, `Ask on connect`, `From a command`); the password box shows only for `Saved`, a command text box for `Command` with a `Test` button.
- `PromptPasswordAsync`: a small dialog with a masked box, `Enter` connects, `Esc` cancels; WinUI `ContentDialog`, Avalonia window, like the SSH host key dialog (P16-06, P16-07).
- One card for both heads.

## Out of scope
- Biometric unlock.

## Tasks
- [x] View-model and headless tests; drive both published exes (recipe 3).
- [x] README line, `CHANGELOG.md` line.

## Acceptance criteria
- Recipe 2 and 3 acceptance for both heads.

## Risks
- Prompt raised while another dialog is open (WinUI allows one `ContentDialog`): queue it as the SSH host key dialog does.

## Open points (defaults)
- None.

## Suggested commit message
Choose the password source in both connection editors and prompt for it on connect

## Done notes
- Premise checked: the password box, `IDialogService` (`PromptAsync`, `ConfirmAsync`, the default `PromptPasswordAsync` of P21-20) and the SSH host key dialogs of both heads exist as stated; the command confirmation of P21-20 already goes through `ConfirmAsync` with the exact command line in its message, so no new confirmation dialog was needed.
- `ConnectionEditorViewModel`: `PasswordSourceIndex` (0 stored, 1 ask, 2 command), `PasswordCommand`, `EffectivePasswordSource` (SQLite is always stored), `PasswordIsStored` / `PasswordIsCommand`, `TestPasswordCommandCommand` (`Test command` button) and `TestHeadline` (the result bar says "The command printed a password." for it). `Validate()` requires a command that the tokenizer accepts (closed quotes) only for the command source. `BuildProfile` writes `PasswordSource` and the trimmed command (null otherwise); `Save` sends an empty password (deletes the stored one) when the source is ask or command, so no stale secret stays behind. The test connection prompts (ask) or runs the command (confirmation first, remembered per profile and command line through `TrustCommandAsync`), cancelling the prompt or declining the command does nothing and shows nothing. The command output is never put in a status, an error or the confirmation text.
- Heads: a source combo above the password box; the password box only for `Saved`, a command box and `Test command` for `From a command`. WinUI `PromptPasswordAsync`: `ContentDialog` with a `PasswordBox` (Enter is the primary `Connect` button, Escape closes without a password); a static gate makes a second `ContentDialog` wait, so two prompts or a prompt and a confirmation cannot collide (dialogs built by other classes, such as the SSH host key one, do not use the gate). Avalonia: `PasswordPromptWindow` (masked box with a bullet, Enter connects, Escape or closing cancels; Enter on the Cancel button cancels).
- Deviations: (1) dialogs raised from the connection editor (command confirmation, password prompt, test errors) opened under the modal editor and could not be reached (found while driving WinUI; the P21-19 error dialog had the same flaw). `MainWindow` now points `WinUiDialogService` at the editor window while it is open, and `AvaloniaDialogService.Owner` is the active visible window instead of always the main window; this also puts the TLS file pickers over the editor. (2) Driving the Avalonia head crashed it with an unhandled `OperationCanceledException` from `TestAsync` when the prompt was cancelled (the toolkit rethrows an `OperationCanceledException` that is not its own cancellation); cancelling is now a plain return, covered by a test. `SessionManager` keeps throwing it, `WorkspaceViewModel.ConnectAsync` catches it. (3) The command confirmation keeps `OK` as the default button (shared `ConfirmAsync`); only a click or Enter on it runs the command.
- Tests: 8 new view-model tests (source rules, validation, save of ask / command / stored, existing profile, command test with exact command and no output leak, declined and failing command, test with ask incl. cancel, test with command), 1 Avalonia headless test (masked box, Enter returns the text, Escape and closing return nothing). Full `dotnet test` of `Squill.Windows.slnx`: 5342 total, 0 failures (the earlier full run of `Squill.slnx` before the card was green too).
- Driven on freshly published exes in a temp folder (profiles `pg-ask` and `pg-cmd` pointing at a closed port, no database needed): WinUI, dark, French: editor shows `Origine du mot de passe` and, for `pg-cmd`, the command box and `Tester la commande`; `Tester la commande` opens the confirmation over the editor with the exact command, `OK` runs it and the bar says `La commande a affiché un mot de passe.`; `Tester` on `pg-ask` opens the masked prompt (UI Automation class `PasswordBox`), `Esc` closes it with no banner. Avalonia, light, English: same source box and command fields, the changed command asks for confirmation (window read from a screenshot, exact command shown), the unchanged one is remembered and runs directly, the `Database password` prompt opens over the editor (read from a screenshot) and `Esc` cancels. Not driven: typing a password into the prompt and Enter (the permission classifier refused to inject text into a password prompt; Enter and the mask are covered by the headless Avalonia test), a real connection through `Ask` or `Command` (no database; the session side is tested in P21-20), the Windows keychain leftover deletion on save (view-model test only). Native AOT checks of both heads: baseline warnings only.
