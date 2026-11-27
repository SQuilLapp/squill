# P21-84 - Presentation and WinUI: collapsible right properties panel with an icon rail

Status: todo
Depends on: none

## Goal
From `docs/ideas.md` (2026-11-19), text reused: let the user close the right-hand table properties panel; when closed it leaves a narrow rail of icons, like the left sidebar does with Explorer / History / Snippets, so the panel can be reopened from the rail.

Exists today (from the idea's own analysis): the left `ActivityRail` (WinUI `MainWindow.xaml` `RailButtonStyle` buttons and the `SidebarPanel` enum in `Views/SidebarPanel.cs`); the right panel is not user-closable: `PropertiesPanel` is shown by the selected explorer node (`Properties.Show`) and hidden by window width (WinUI `SyncProperties`).

## Design decisions (final)
- Shared part (this card): `AppSettings.PropertiesPanelClosed` (bool, default false) and a `WorkspaceViewModel.IsPropertiesPanelClosed` with `TogglePropertiesPanelCommand` (palette action, key `Ctrl+Alt+P` registered in the keymap defaults if P21-78 is done, else hard-coded like other keys).
- WinUI: a close button in the panel header; when closed, a right rail column (same width and style as the left rail) with a `Properties` icon button that reopens it; the width rule (`SyncProperties`) still hides the panel on a narrow window but keeps the user's choice; the splitter column collapses with the panel.

## Out of scope
- Avalonia (P21-85), more panels on the right rail.

## Tasks
- [ ] Presentation tests (setting round trip, command); WinUI text-check tests; publish and drive (recipe 3): close, restart (stays closed), reopen from the rail, narrow window.

## Acceptance criteria
- Recipe 2 and 3 acceptance (WinUI).

## Risks
- `SyncProperties` and the user flag fighting: one function decides visibility from both inputs.

## Open points (defaults)
- None.

## Suggested commit message
Let the user close the WinUI properties panel to a right icon rail
