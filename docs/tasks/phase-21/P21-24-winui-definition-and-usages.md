# P21-24 - WinUI: F12, Ctrl+click and usage highlights in the LoomEditor

Status: done
Depends on: P21-23

## Goal
Bind the P21-23 commands in the WinUI head.

Exists today: `src/Squill.App.WinUI/Editors/LoomSqlEditor.cs` (the `ISqlEditor` over LoomEditor, decorations used for highlighting and squiggles), key handling in `QueryTabView.cs`.

## Design decisions (final)
- `F12`, `Shift+F12`, `Alt+F7`, `Ctrl+Shift+B` handled in the editor key handler; `Ctrl+click` if LoomEditor exposes pointer position to offset (check its API first); if it does not, `Ctrl+click` is `not built` and the Done notes say so (the keys still work).
- `SetHighlights`: a decoration layer with a token brush (`SquillAccentSubtleBrush` or the closest existing token), repainted on theme change.

## Out of scope
- Avalonia (P21-25).

## Tasks
- [x] Text-check tests (P20-38 pattern) for the key names and `SetHighlights`; publish and drive (recipe 3): F12 on an alias, on a CTE, on a table (explorer selection), usages list, dark and light.

## Acceptance criteria
- Recipe 2 and 3 acceptance (WinUI).

## Risks
- LoomEditor decoration count on a large script: cap highlights at 1,000 ranges.

## Open points (defaults)
- None.

## Suggested commit message
Bind go to definition and find usages in the WinUI editor

## Done notes
- Premise checked: `LoomSqlEditor` (decorations for highlighting, markers for squiggles) and the key handler (`QueryTabView.OnEditorPreviewKeyDown`, on the editor host) exist. Deviation: the WinUI explorer did not follow `ExplorerTreeViewModel.SelectedNode` set by code, so go to definition on a table selected nothing visible; `SidebarView` now mirrors a code-made selection (`OnExplorerPropertyChanged`, then `StartBringIntoView`). The properties panel opens on its own once the node is selected.
- Keys in `QueryTabView`: `F12`, `Shift+F12`, `Alt+F7`, `Ctrl+Shift+B`, `Esc` (clears highlights, not marked handled so the editor still closes its popups). `SetHighlights` in `LoomSqlEditor`: a second `DecorationsCollection` with a registered class (`ClassName`, background and border, accent-soft and accent-line values of `SquillTokens.xaml` for dark and light; LoomEditor picks the colors per theme itself, so no repaint hook is needed), capped at 1,000 ranges. `InlineClassName` draws nothing for a background (found by driving), `ClassName` does.
- `Ctrl+click`: LoomEditor exposes no pointer-to-offset API, but its own handler moves the caret to the click. `LoomSqlEditor` listens to `PointerPressed` (`handledEventsToo`) and raises `DefinitionRequested` with Ctrl held, and the view runs `GoToDefinitionCommand` on the caret. Built but `not driven` (`Drive-Window.ps1` cannot click with a modifier).
- Tests: `DefinitionKeysTests` (text checks: keys, commands, cap, decoration layer, sidebar sync); `LoomSqlEditor.cs` is now linked into the WinUI test project.
- Verified on the published exe (temp folder, deleted): F12 on an alias (caret and selection on the declaration), Shift+F12 (usages list, purple boxes on every use), Esc (boxes cleared), F12 on a table (explorer selection and properties panel), F12 on a CTE and its usages (light), `Ctrl+Shift+B` (designer tab opened). Dark was captured for the alias and Esc steps, light for the table, CTE and designer steps; all in French (not driven in English; `Alt+F7` not driven).
- Builds `Squill.slnx` and `Squill.Windows.slnx` 0 warnings; `dotnet test --solution Squill.slnx` 5332 total, 0 failed (Avalonia 204 green); `eng/check-aot-warnings.ps1 -Head WinUI`: only the 4 baseline warnings.
