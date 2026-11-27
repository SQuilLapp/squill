# P21-54 - Presentation: binary values as a hex dump or an image

Status: todo
Depends on: none

## Goal
A binary cell (`bytea`, `BLOB`, `varbinary`) can be viewed as a paged hex dump and, when it is an image, as a picture.

Exists today: binary cells are drawn as `0x...` truncated to `CellFormatter.MaxHexBytes`, edited as `0x` hex text (`CellValueParser.ParseHex`), exported in full as hex; the preview pane shows text only.

## Design decisions (final)
- `BinaryPreviewViewModel`: `HexRows` paged (16 bytes per row: offset, hex, ASCII), 4,096 rows per page; `Size`; `DetectedType` from magic bytes (PNG, JPEG, GIF, BMP, WebP, ICO, PDF, ZIP, gzip, SQLite) - pure `BinarySniffer`.
- Image: for PNG/JPEG/GIF/BMP/WebP the view-model exposes the bytes and dimensions read from the header (no decoding in Presentation); heads decode with their own bitmap types. Images over 50 MB are not decoded.
- Commands: `Save to file...` (`PickSaveFileAsync`, extension from the detected type), `Load from file...` when the cell is editable (replaces the value, size limit 100 MB, goes through the normal pending-edit path).
- The preview pane opens for binary cells (today it does not).

## Out of scope
- Heads (P21-55, P21-56), editing bytes in the hex view.

## Tasks
- [ ] Tests: sniffer per type, header dimensions (PNG, JPEG SOF, GIF, BMP), paging, save/load commands through fakes, size limits.

## Acceptance criteria
- Recipe 2 common acceptance.

## Risks
- Drivers may stream large BLOBs lazily: the value is already a `byte[]` in the grid today (max grid rows); no new read path.

## Open points (defaults)
- None.

## Suggested commit message
Preview binary values as a hex dump or an image in the view-model
