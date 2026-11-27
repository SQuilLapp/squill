# P21-21 - Password sources in both heads (editor choice and the password prompt)

Status: todo
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
- [ ] View-model and headless tests; drive both published exes (recipe 3).
- [ ] README line, `CHANGELOG.md` line.

## Acceptance criteria
- Recipe 2 and 3 acceptance for both heads.

## Risks
- Prompt raised while another dialog is open (WinUI allows one `ContentDialog`): queue it as the SSH host key dialog does.

## Open points (defaults)
- None.

## Suggested commit message
Choose the password source in both connection editors and prompt for it on connect
