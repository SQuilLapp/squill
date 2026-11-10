# P13-18 - Avalonia head: Expliquer and Analyser buttons, plan tab host, tab strip, status bar

Status: todo
Depends on: P13-14, P13-15, P9-11

## Goal
Twin of P13-15 for the Avalonia head. Look at how the diagram tab is hosted: `MainWindow.axaml(.cs)`, `Views/DiagramTabView.axaml(.cs)`, `Views/WorkspaceTabList.cs`, `Views/StatusBar.axaml(.cs)`, and the query toolbar in `Views/QueryTabView.axaml`.

## Design decisions (final)
- Same behaviour and layout as P13-15 (read it, copy its decisions, do not re-decide): toolbar buttons `Expliquer` and `Analyser` + `Ctrl+Maj+E` chip with the disabled-reason tooltips; plan tab headers in the strip (colored square, name, muted `· Plan`, close); `Views/PlanTabView.axaml(.cs)` with the same toolbar row (segmented control, command text, summary figures, pills, zoom, `Relancer`) and a `Texte` body (read-only mono `TextBox`, `IsReadOnly`, selectable); status bar switches to the plan status line; key bindings only valid in the right tab.
- Avalonia styles from P9-05, token brushes only; missing token -> stop and propose a token card.

## Out of scope
- Table, details, graph (P13-19, P13-20), WinUI.

## Tasks
- [ ] Buttons, tab host, toolbar, `Texte` body, strip and status bar, localization labels.
- [ ] Capture the toolbar light and dark against M06 (tools of P9-02; Avalonia can also be rendered headless as P9-19 did) and list what was not compared.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` 0 warnings; `rtk bypass dotnet test --solution Squill.slnx -c Release` green; Avalonia AOT publish zero IL2xxx/IL3xxx.
- To check by hand: `Expliquer` on a SQLite file opens the plan tab with the raw text.

## Suggested commit message
Add the plan tab host and Expliquer / Analyser buttons to the Avalonia head
