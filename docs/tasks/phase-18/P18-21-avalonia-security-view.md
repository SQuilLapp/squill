# P18-21 - Avalonia head: the security view

Status: done
Depends on: P18-20

## Goal
The same M11 view in the Avalonia head. Recipe 6.

## Design decisions (final)
- `Views/SecurityView.axaml(.cs)`; the matrix as a custom-drawn control like the grid (same glyph states and numbers as WinUI), `ToolTip` on inherited cells, `KeyBinding`s, same wiring.

## Tasks
- [x] View, matrix control, wiring.
- [x] Headless tests in `tests/Squill.App.Avalonia.Tests` (hit test, key toggles, both themes render different pixels).

## Acceptance criteria
- Common acceptance (recipe 7) with the Avalonia AOT publish; compare as far as a capture allows and list the rest.

## Suggested commit message
Add the Avalonia security view

## Notes
- Matrix drawn by `Controls/PrivilegeMatrixCanvas` (one `Render` pass, shared `SecurityFormat` copy of the WinUI one); cell tooltips follow the pointer.
- Avalonia AOT publish: no IL2xxx/IL3xxx from Squill code (only the known third-party SqlClient trim warnings). Pixel comparison with the M11 capture is left to P18-31.
