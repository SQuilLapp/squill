# P19-05 - Say when the sessions and privileges lists are cut (Avalonia)

Status: todo
Depends on: P19-04

## Goal
Same defect as P19-04 in the Avalonia head: `SessionsMonitorView.cs:37` (`private const int MaxRows = 400`, `vm.Rows.Take(MaxRows)` at line 471) and `Controls/PrivilegeMatrixCanvas.cs:29` (`internal const int MaxRows = 400`, `RowCount` at line 66) cut the lists silently. P19-04 added `MaxShownRows`, `HiddenRowCount` and the catalog keys `activity.rowsCut` / `security.rowsCut`.

## Design decisions (final)
- Delete both head constants and use `SessionsMonitorViewModel.MaxShownRows` / `SecurityViewModel.MaxShownRows`; the canvas, its hit test and its keyboard movement keep one bound.
- A `TextBlock` under each list bound to the notice text, visible only when `HiddenRowCount > 0`, muted token color (no hard-coded color). Same wording as WinUI (shared catalog keys).
- Reword the two `shortcut:` comments to mention the notice; behavior up to 400 rows is unchanged.

## Tasks
- [ ] Head changes as above.
- [ ] Headless tests in `tests/Squill.App.Avalonia.Tests` (`[Collection("Headless")]`, existing view test patterns): a view-model with 401 rows shows the notice, with 400 it does not; the matrix hit test cannot select row 401.
- [ ] Hand check list in the Done notes (owner): same container scenario as P19-04.

## Acceptance criteria
- Common acceptance (recipe 4) with `Squill.slnx` and the Avalonia AOT publish (`rtk dotnet publish src/Squill.App.Avalonia -c Release -r win-x64`), the app starts; `LayoutOverlapTests` still green (the notice must not overlap other text).

## Risks
- The notice adds a line under the list: check `LayoutOverlapTests` and the Avalonia status bar thread guard (P18-34) are not affected.

## Suggested commit message
Tell the user when the Avalonia sessions and privileges lists show only the first rows
