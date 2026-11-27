# P21-55 - WinUI: hex and image viewer in the preview pane

Status: todo
Depends on: P21-54

## Goal
Draw `BinaryPreviewViewModel` in the WinUI preview pane.

Exists today: `Views/CellPreviewPanel.cs`.

## Design decisions (final)
- `Hex | Image` toggle (Image only when detected); hex as a virtualized mono list; image with `BitmapImage.SetSourceAsync` over an in-memory stream, fit to the pane with a `1:1` button and the dimensions shown; a decode failure shows the message and falls back to hex.

## Out of scope
- Avalonia (P21-56).

## Tasks
- [ ] Text-check tests; publish and drive (recipe 3) with a SQLite table holding a PNG, a JPEG and random bytes (added to the verification scenario script).

## Acceptance criteria
- Recipe 2 and 3 acceptance (WinUI).

## Risks
- None specific.

## Open points (defaults)
- None.

## Suggested commit message
Show the hex and image viewer in the WinUI preview pane
