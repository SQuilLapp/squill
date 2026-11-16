# P16-07 - Avalonia head: SSH host key dialog

Status: todo
Depends on: P16-06

## Goal
The Avalonia twin of P16-06: replace the temporary "decline" implementation of `ConfirmSshHostKeyAsync` with a real window.

## Design decisions (final)
- Same content, buttons, defaults and rules as P16-06 (`Annuler` default, `Entrée` / `Echap` / closing cancel, fingerprint selectable in a monospace box), built like the Avalonia production confirmation window of P15-18 (look at how it is created, owned by the main window and made modal; reuse that helper).
- Texts only through `UiText` (keys of P16-05).

## Out of scope
- WinUI, new texts.

## Tasks
- [ ] Window and service member; remove the temporary implementation.
- [ ] Headless Avalonia test (`tests/Squill.App.Avalonia.Tests` pattern of the production window): default button is cancel, `Entrée` cancels, the trust button returns true, closing returns false.

## Acceptance criteria
- Common acceptance with `Squill.slnx`; Avalonia AOT publish with zero IL2xxx/IL3xxx warnings (`rtk dotnet publish src/Squill.App.Avalonia -c Release -r win-x64`, background, run once and close).

## Suggested commit message
Add the SSH host key question window to the Avalonia head
