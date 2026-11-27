# P21-76 - WinUI: user and role editor in the security view

Status: todo
Depends on: P21-75

## Goal
Show the P21-75 commands and editor in the WinUI security view.

Exists today: `Views/SecurityView.cs` (WinUI).

## Design decisions (final)
- Toolbar buttons over the role list; the editor as a side panel replacing the matrix while open (fields, password boxes, member lists with add from a filtered combo), `Add to script` / `Cancel`.

## Out of scope
- Avalonia (P21-77).

## Tasks
- [ ] Text-check tests; publish and drive (recipe 3) on PostgreSQL and MySQL containers: create a user, add to a role, drop it.

## Acceptance criteria
- Recipe 2 and 3 acceptance (WinUI).

## Risks
- None specific.

## Open points (defaults)
- None.

## Suggested commit message
Show the user and role editor in the WinUI security view
