# P21-46 - WinUI: the data compare view

Status: todo
Depends on: P21-45

## Goal
Draw `DataCompareViewModel` in the WinUI head.

Exists today: `Views/SchemaCompareView.cs` (pickers, toolbar, script pane) as the layout pattern.

## Design decisions (final)
- Same top bar as the schema compare view (source, swap, target, `Compare`), a summary strip (identical / changed / only source / only target), a differences list with a kind filter, a detail pane showing source and target values side by side with changed cells highlighted, the script pane with `Run` and `Save .sql`.

## Out of scope
- Avalonia (P21-47).

## Tasks
- [ ] Text-check tests; publish and drive (recipe 3) on two SQLite files prepared by the verification scenario script.

## Acceptance criteria
- Recipe 2 and 3 acceptance (WinUI).

## Risks
- None specific.

## Open points (defaults)
- None.

## Suggested commit message
Show the data compare view in the WinUI head
