# P12-10 - WinUI head: variables manager

Status: todo
Depends on: P12-09

## Goal
A screen to list, create, edit, rename and delete variables per scope, with a view of the effective values for the active connection.

## Design decisions (final)
- Presentation first (same card): `VariablesManagerViewModel` over `IVariableService`: scope selector (Global, Environment with the four kinds, Connection with a connection picker defaulting to the active one), a list of rows (`Name`, `Value` masked when secret, `Secret`, `Allow raw`, `Description`), commands Add, Edit, Rename, Delete (with confirmation through `IDialogService`), a "Show effective values" toggle that lists the resolved variables of the selected connection with the scope each one comes from and the shadowed ones. Filter box. Errors through `variables.error.*`.
- WinUI view: a page opened from the menu (Tools or Edit menu entry "Variables", read `docs/design/visual-spec.md` for where menu items live) and from the quick action "Open variables manager"; layout in the style of the settings and connection manager (P9-18, P9-24): scope list left, grid right. Secret values are shown as dots with an explicit "Reveal" button per row (requires no extra authentication; it only shows what the user typed) and are never copied to the clipboard by Copy actions on the row.
- Uses the shared control styles (P9-06) and the localization rules; keyboard: Delete, F2 rename, Enter edit.

## Out of scope
- Avalonia (P12-12), import/export of variable sets (follow-up).

## Tasks
- [ ] View-model and tests (pure logic: scopes, effective values, masking, filters, delete confirmation), WinUI view, menu entry, catalog keys fr/en.
- [ ] Screenshots en/fr light/dark of the manager with the four scopes; "to check by hand" list.

## Acceptance criteria
- `rtk dotnet build Squill.Windows.slnx -c Release -warnaserror` 0 warnings; WinUI AOT publish clean; tests green; no process left running.

## Suggested commit message
Add the variables manager to the WinUI head
