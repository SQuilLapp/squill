# P16-06 - WinUI head: SSH host key dialog

Status: todo
Depends on: P16-05

## Goal
Replace the temporary "decline" implementation of `ConfirmSshHostKeyAsync` in the WinUI head with a real dialog.

## Design decisions (final)
- Same dialog family as the other confirmation dialogs of the head (the production confirmation and transaction question of P15-16 are the model: look at how they are hosted and styled with the shared tokens, no new styling system). Content: title and body from the P16-05 keys (host, port, algorithm), the fingerprint in a selectable monospace text box (the user compares it with what the administrator gave them), two buttons `Faire confiance` / `Annuler` (en: `Trust` / `Cancel`).
- `Annuler` is the default and the accept button of the dialog is never the default: `Entrée` cancels, `Echap` cancels, closing the window cancels. Only a click (or `Alt` mnemonic on the trust button) trusts.
- Shown on the UI thread (the service marshals like the other dialogs); a second request while one is open waits (the existing queue of the dialog service, reuse it).
- Texts only through `UiText`; no new catalog keys beyond P16-05's.

## Out of scope
- Avalonia (P16-07), new catalog texts, a fingerprint in other formats.

## Tasks
- [ ] Dialog and service member; remove the temporary implementation.
- [ ] Tests where `tests/Squill.App.WinUI.Tests` already has a pattern (a view-model or model test of the button defaults); what cannot be driven headless is listed under "To check by hand": fingerprint selectable and copyable, `Entrée` cancels, French and English text not clipped, dark theme contrast.

## Acceptance criteria
- Common acceptance with `Squill.Windows.slnx`, WinUI AOT publish with zero IL2xxx/IL3xxx warnings (`rtk dotnet publish src/Squill.App.WinUI -c Release -r win-x64`, in the background, then run the exe once and close it).

## Suggested commit message
Add the SSH host key question dialog to the WinUI head
