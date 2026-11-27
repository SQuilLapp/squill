# P21-71 - WinUI: mapping review step and equivalent rows in the schema compare view

Status: todo
Depends on: P21-70

## Goal
Show the cross-engine mode of P21-70 in the WinUI schema compare view.

Exists today: `Views/SchemaCompareView.cs` (WinUI).

## Design decisions (final)
- The mapping review as a panel replacing the result list until accepted (rows with accept check and override combo built in code), then the usual layout with `Equivalent` rows muted and their mapping in the detail pane.

## Out of scope
- Avalonia (P21-72).

## Tasks
- [ ] Text-check tests; publish and drive (recipe 3): SQLite scenario -> empty PostgreSQL container database, accept, run the script, compare again.

## Acceptance criteria
- Recipe 2 and 3 acceptance (WinUI).

## Risks
- None specific.

## Open points (defaults)
- None.

## Suggested commit message
Show the cross-engine mapping review in the WinUI schema compare view
