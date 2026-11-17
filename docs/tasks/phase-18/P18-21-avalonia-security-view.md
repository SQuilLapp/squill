# P18-21 - Avalonia head: the security view

Status: todo
Depends on: P18-20

## Goal
The same M11 view in the Avalonia head. Recipe 6.

## Design decisions (final)
- `Views/SecurityView.axaml(.cs)`; the matrix as a custom-drawn control like the grid (same glyph states and numbers as WinUI), `ToolTip` on inherited cells, `KeyBinding`s, same wiring.

## Tasks
- [ ] View, matrix control, wiring.
- [ ] Headless tests in `tests/Squill.App.Avalonia.Tests` (hit test, key toggles, both themes render different pixels).

## Acceptance criteria
- Common acceptance (recipe 7) with the Avalonia AOT publish; compare as far as a capture allows and list the rest.

## Suggested commit message
Add the Avalonia security view
