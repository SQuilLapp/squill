# P12-12 - Avalonia head: variables manager

Status: todo
Depends on: P12-10, P12-11

## Goal
The variables manager of P12-10 in the Avalonia head, reusing `VariablesManagerViewModel` unchanged.

## Design decisions (final)
- A window opened from the menu entry and from the quick action "Open variables manager", in the layout of the Avalonia settings and connection manager windows (P9-17, P9-23): scope list left, grid right, effective-values toggle, filter, reveal button for secrets, same keyboard shortcuts.
- No logic in the view: anything needed beyond P12-10 goes into the shared view-model with a test there.
- Strings through the catalog (the keys exist from P12-10).

## Out of scope
- Changes to the view-model contract (stop and report if one is needed).

## Tasks
- [ ] Window, menu entry, style, screenshots en/fr light/dark, "to check by hand" list.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` 0 warnings; Avalonia AOT publish clean; tests green; no process left running.

## Suggested commit message
Add the variables manager to the Avalonia head
