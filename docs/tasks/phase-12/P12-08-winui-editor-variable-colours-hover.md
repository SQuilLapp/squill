# P12-08 - WinUI head: variables coloured in the editor and hover popup

Status: todo
Depends on: P12-07

## Goal
In `LoomSqlEditor` (the real editor of the WinUI head) each `{{name}}` is coloured by state and hovering shows the popup text. Limits of the package are in [variables-spec.md](variables-spec.md) section 7; read it first.

## Design decisions (final)
- Colours: add design tokens `variable.resolved` (green), `variable.unresolved` (orange), `variable.secret` (purple) with light and dark values to the design system and the WinUI resources (read `docs/design/visual-spec.md` and the P9-01 token pipeline; run the generator `XamlGenerator`, do not edit generated files by hand). The decoration styles are registered with `CustomDecorationStyles.Register` like the syntax kinds in `RegisterStyles()`, names `squill-variable-resolved|unresolved|secret`, foreground only (the package offers no background in `CustomDecorationStyle`; if the installed version does, add a 15 % tint and say so in the report).
- `LoomSqlEditor.SetVariables(marks)`: a **separate** `DecorationsCollection` (`_variables`) replaced as a whole on each call (the number of variables is small), so syntax decorations are not redrawn. Offsets to `Range` through `Model.GetPositionAt`. The syntax highlighter already emits `HighlightKind.Variable` (P12-02): map it to no decoration (the variable collection owns the colour), to avoid two colours on one range.
- Hover: extend the existing `SqlProviders` hover so that when the hovered offset is inside a variable mark it returns `Hover([MarkdownString(model.ToMarkdown())], range)` built from `VariablePopupModel` (name, state, value or masked value, scope, shadowed scopes, and for an unresolved one the sentence "Click the variable or press Ctrl+. to create it"). Hover for variables wins over the SQL hover (the masked text would give none anyway).
- Because actions cannot live in the hover (spec section 7), the popup states how to act: click, `Ctrl+.`, chip row. This is the documented deviation from Postman; write it in the report.
- Debounce: reuse the 120 ms timer of `LoomSqlEditor`; the controller of P12-07 is told the visible range on `ScrollChanged` / `LayoutChanged` (reuse `VisibleLines()`).
- `PlainSqlEditor` (fallback without LoomEditor, `#if` build without `SQUILL_LOOMEDITOR`) keeps no colouring; `SetVariables` is a no-op there and the chip row (P12-09) still works.

## Out of scope
- Click, flyout, chip row, dialogs (P12-09), manager (P12-10), Avalonia.

## Tasks
- [ ] Tokens + resources, decorations, hover, wiring of `VariableEditorController` in the query tab view, catalog keys already added in P12-07 reused.
- [ ] Tests: unit tests of mark-to-range mapping and popup Markdown (no UI); a headless test constructing `LoomSqlEditor` if the existing WinUI tests already do (follow `tests/Squill.App.WinUI.Tests` if present, otherwise none).
- [ ] Visual check with `tools/visual` (verification-recipe section 2) with `SQUILL_LANGUAGE=en` and `fr`, light and dark: editor with a resolved, an unresolved and a secret variable; hover on each. List "to check by hand" for the hover (pointer driving may not be automatable).

## Acceptance criteria
- `rtk dotnet build Squill.Windows.slnx -c Release -warnaserror` 0 warnings; WinUI AOT publish `rtk dotnet publish src/Squill.App.WinUI -c Release -r win-x64` with zero IL2xxx/IL3xxx; tests green.
- No process left running (app, build server).

## Suggested commit message
Colour query variables in the WinUI editor and explain them on hover
