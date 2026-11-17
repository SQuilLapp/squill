# P18-10 - Avalonia head: the schema compare view

Status: todo
Depends on: P18-09

## Goal
The same M09 view in the Avalonia head as P18-09. Recipe 6.

## Design decisions (final)
- `Views/SchemaCompareView.axaml(.cs)`; DDL panes as one custom control over `AlignedLine`s (same numbers as WinUI); script pane with AvaloniaEdit as the query tab does.
- Menu and explorer context item wired; keys `F7`, `Maj+F7`, `Ctrl+S`, `Ctrl+Entrée` as `KeyBinding`s.

## Tasks
- [ ] View, DDL pane control, wiring.
- [ ] Headless tests in `tests/Squill.App.Avalonia.Tests` (marks, filler, both themes render different pixels, execute button state follows the target environment).

## Acceptance criteria
- Common acceptance (recipe 7) with the Avalonia AOT publish; compare as far as a capture allows and list the rest.

## Suggested commit message
Add the Avalonia schema compare view
