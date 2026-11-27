# P21-83 - Avalonia: apply custom themes at runtime

Status: todo
Depends on: P21-81, P21-82

## Goal
Same as P21-82 in the Avalonia head (replace the brushes in the theme dictionaries; `DynamicResource` users update), then README and changelog lines.

Exists today: generated AXAML token dictionaries, `DynamicResource` brushes, custom-drawn canvases.

## Design decisions (final)
- Same settings UI; canvases invalidate on the theme-changed event.

## Out of scope
- WinUI.

## Tasks
- [ ] Headless tests; publish and drive (recipe 3).
- [ ] README "Language and screens" line, `CHANGELOG.md` line.

## Acceptance criteria
- Recipe 2 and 3 acceptance (Avalonia).

## Risks
- None beyond P21-82.

## Open points (defaults)
- None.

## Suggested commit message
Apply custom themes at runtime in the Avalonia head
