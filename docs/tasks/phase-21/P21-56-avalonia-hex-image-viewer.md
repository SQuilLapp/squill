# P21-56 - Avalonia: hex and image viewer in the preview pane

Status: todo
Depends on: P21-54, P21-55

## Goal
Same as P21-55 in the Avalonia head, then README and changelog lines.

Exists today: the Avalonia preview pane.

## Design decisions (final)
- Same toggle; image with `Avalonia.Media.Imaging.Bitmap` from a stream (WebP may not decode: fall back to hex and say so).

## Out of scope
- WinUI.

## Tasks
- [ ] Headless tests; publish and drive (recipe 3).
- [ ] README line, `CHANGELOG.md` line.

## Acceptance criteria
- Recipe 2 and 3 acceptance (Avalonia).

## Risks
- None beyond P21-55.

## Open points (defaults)
- None.

## Suggested commit message
Show the hex and image viewer in the Avalonia preview pane
