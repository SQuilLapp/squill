# P12-11 - Avalonia head: coloured variables, hover popup with actions, chip row

Status: todo
Depends on: P12-09

## Goal
The Avalonia head does the same as P12-08 and P12-09, and unlike LoomEditor it can put the actions inside the hover popup, as Postman does.

## Design decisions (final)
- `SqlColorizer` (`DocumentColorizingTransformer`, `src/Squill.App.Avalonia/Editors/SqlColorizer.cs`) receives the marks of `ISqlEditor.SetVariables` and colours the ranges with brushes from design tokens `variable.resolved|unresolved|secret` (same tokens and values as WinUI: add them to `Tokens.cs`/the Avalonia resources via the generator, no hand-edited generated file); the `HighlightKind.Variable` span from the highlighter is ignored by the colorizer (marks own the colour). Redraw only the affected lines.
- Hover: `PointerMoved` on the `TextView` with `GetPositionFromPoint` / offset, 300 ms dwell, opens a `Popup` (custom, not `ToolTip`, so it can hold buttons) showing the `VariablePopupModel` (P12-07) and the `VariableQuickActions` as buttons (Create in Global / Environment / Connection, Edit value, Set for this tab). The popup stays open while the pointer is over the variable or the popup and closes on `Escape` or pointer leave after 200 ms.
- Click and `Ctrl+.` raise `VariableActivated` and open the same actions as a `MenuFlyout`.
- Chip row under the editor with the same view-model and tokens; dialog `PromptVariableAsync` as a window in the style of the Avalonia connection editor (P9-17) with the shared styles (P9-05); the parameter prompt labels the variables.
- Strings through `UiText`, same keys as WinUI.

## Out of scope
- Manager window (P12-12).

## Tasks
- [ ] Colorizer update, hover popup, flyout, chip row, dialog, parameter prompt label.
- [ ] Tests: pure logic (dwell decision, marks to line segments); screenshots en/fr light/dark through `tools/visual`; "to check by hand" for hover with the exact movements.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` 0 warnings; `rtk dotnet publish src/Squill.App.Avalonia -c Release -r win-x64` with zero IL2xxx/IL3xxx; tests green; no process left running.

## Suggested commit message
Colour query variables in the Avalonia editor with a hover popup and actions
