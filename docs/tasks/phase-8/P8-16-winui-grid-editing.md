# P8-16 - WinUI head: in-place cell editing in the grid

Status: todo
Depends on: P8-12, P5-12

## Goal
The WinUI counterpart of P8-14 on the Win2D grid.

## Design decisions (final)
- Read the Win2D grid control of P5-10 (`src/Squill.App.WinUI/`, find it with `rtk grep -l "CanvasControl"`) and how P5-12 passes the result and selection to it. Pass `DisplayGrid`, `Selection` and `EditController` of the result (P8-12); a null controller means read-only as before.
- Same editing behaviour, key mapping (table of P8-11) and drawing rules (state tints, strikethrough, issue border, muted read-only columns) as P8-14, using the same tokens. The in-place editor is a `TextBox` placed over the cell in a `Canvas` overlay, bound to `EditText` (`x:Bind` mode `TwoWay`), with the error shown as a red border and tooltip; clipboard access through `Windows.ApplicationModel.DataTransfer.Clipboard` for `Ctrl+V`, errors through `IDialogService.ShowErrorAsync`.
- Do not duplicate logic that lives in `GridEditController`; only map input events to it and draw.
- Build and test with `Squill.Windows.slnx`.

## Out of scope
- Toolbar and commit buttons (P8-17), anything in the Avalonia head.

## Tasks
- [ ] Implement the editing overlay, key handling and drawing.
- [ ] Tests in `tests/Squill.App.WinUI.Tests` for the testable parts (cell rectangle to overlay position math, row state to brush selection). State in the card report what could not be exercised without UI automation.

## Acceptance criteria
- `rtk dotnet build Squill.Windows.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.Windows.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).
- `rtk dotnet publish src/Squill.App.WinUI -c Release -r win-x64` succeeds with zero IL2xxx/IL3xxx warnings.

## Suggested commit message
Add in-place cell editing to the WinUI grid
