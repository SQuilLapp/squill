# P18-10 - Avalonia head: the schema compare view

Status: done
Depends on: P18-09

## Goal
The same M09 view in the Avalonia head as P18-09. Recipe 6.

## Design decisions (final)
- `Views/SchemaCompareView.axaml(.cs)`; DDL panes as one custom control over `AlignedLine`s (same numbers as WinUI); script pane with AvaloniaEdit as the query tab does.
- Menu and explorer context item wired; keys `F7`, `Maj+F7`, `Ctrl+S`, `Ctrl+Entrée` as `KeyBinding`s.

## Tasks
- [x] View, DDL pane control, wiring.
- [x] Headless tests in `tests/Squill.App.Avalonia.Tests` (marks, filler, both themes render different pixels, execute button state follows the target environment).

## Acceptance criteria
- Common acceptance (recipe 7) with the Avalonia AOT publish; compare as far as a capture allows and list the rest.

## Suggested commit message
Add the Avalonia schema compare view

## Report
- Built in code like the WinUI view (no `.axaml`): `Views/SchemaCompareView.cs`, `Controls/DdlPane.cs` (custom-drawn rows, tints, `+` / `-` marks, hatched filler, shared scroll), `Views/CompareFormat.cs` (a copy of the WinUI helper, tested). The script editor is `AvaloniaSqlEditor` (LoomEditor), editable.
- Wired: tab host, `WorkspaceTabList`, `Base de donnees` menu item (disabled reason as tooltip), explorer connection context item, status bar (`STAGING -> PRODUCTION`, counts, both server versions), `F7` / `Maj+F7` / `Ctrl+S` / `Ctrl+Entree` as `KeyBinding`s on the view (active while focus is inside it).
- Verified: both solutions build with 0 warnings, all tests green (headless: marks, filler, equal row counts, execute button follows the target environment, light and dark differ), Avalonia AOT publish without IL2xxx/IL3xxx from our code (only the existing third-party IL2104).
- To check by hand (no capture was taken): the M09 comparison light / dark with two live databases; a Production target uses the `outline-danger` button style, not the solid danger fill of WinUI; horizontal scroll of long DDL lines (not implemented, text is clipped).
