# P20-35 - Avalonia: a "Pasting..." popup with an indeterminate progress bar during a long paste

Status: todo
Depends on: P20-32, P20-33, P20-34

Note (P20-40): the in-place editor already handles `PastingFromClipboard` (and a tunnel `KeyDown` while read-only) since P20-40 (`DataGridView.PasteIntoEditor`). For the editor, wrap that path with the overlay for a long text instead of adding a second handler; the design below stays for the preview pane.

## Goal
Bring P20-34 to the Avalonia head: a paste of at least `LongPaste.Threshold` characters into the grid, the in-place cell editor or the preview pane shows a popup over the result area with an indeterminate progress bar and `Pasting…` / `Collage en cours…` (keys of P20-34), closed when the paste is done; the preview pane shows a working strip while `CellPreviewViewModel.IsWorking`.

Files today: `src/Squill.App.Avalonia/Views/QueryTabView.axaml` (+ `.axaml.cs`: the result area with the grid and `CellPreviewPanel`, an indeterminate `ProgressBar` at line ~252), `src/Squill.App.Avalonia/Controls/DataGridCanvas.cs` (`PasteAsync`, line ~735, reads `TopLevel.Clipboard.TryGetTextAsync()`), `src/Squill.App.Avalonia/Controls/DataGridView.cs` (`_editor`), `src/Squill.App.Avalonia/Views/CellPreviewPanel.cs` (`_text`), precedent `src/Squill.App.Avalonia/Views/ColumnMenu.cs` (indeterminate `ProgressBar`).

## Design decisions (final)
- Same behaviour, threshold, keys, placement (over the grid and the preview pane, not the tool bars) and events as P20-34; only the Avalonia mechanics differ:
  - New `src/Squill.App.Avalonia/Views/BusyOverlay.cs`, `internal sealed class BusyOverlay : Panel` (one-line `<summary>`), built in code: scrim `Border` (`SquillBgBaseBrush`, `Opacity` 0.6, catches the pointer), centred card (`SquillBgOverlayBrush`, `SquillBorderBrush`, corner radius 8, padding 16) with a `TextBlock` and a `ProgressBar { IsIndeterminate = true, Width = 220 }`; brushes as dynamic resources like the other Avalonia views; `AutomationProperties.Name` = `results.pasting`; `Show()` / `Hide()` with the same counter as WinUI. Added last to the result area of `QueryTabView`, spanning the grid and the pane.
  - Grid `Ctrl+V`: in `DataGridCanvas.PasteAsync`, `LongPaste.IsLong(text)` -> `BusyChanged(true)`, `await edit.PasteAsync(text, CancellationToken.None)`, `BusyChanged(false)` in a `finally`; key handlers ignore input while busy.
  - In-place editor and preview pane: handle `TextBox.PastingFromClipboard` (`e.Handled = true`), read `TopLevel.GetTopLevel(box)?.Clipboard` with `TryGetTextAsync()`, short text -> `box.SelectedText = text`; long text -> show the overlay, `await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render)` so it is rendered, `box.SelectedText = text`, hide.
  - Pane strip: a 2 px indeterminate `ProgressBar` under the pane header bound to `IsWorking` (`preview.working` as automation name).
- Avalonia runs the indeterminate animation on the UI thread: the bar may stop while the native-side text insert runs. Accept it: the popup still tells the user what happens; record what the drive shows.

## Out of scope
- The WinUI head (P20-34), cancelling, a determinate percentage, the SQL editor and the import wizard.

## Tasks
- [ ] Tests first:
  - `tests/Squill.App.Avalonia.Tests/LongPasteUiTests.cs` (headless, like `GridEditingUiTests.cs`): a `BusyOverlay` shown then hidden twice-nested stays visible until the last `Hide`; its text is `UiText.Get("results.pasting")`; a grid paste of a text at the threshold through the canvas path raises `BusyChanged(true)` then `(false)` and sets the cells; a short paste raises nothing. Where headless input cannot reach the clipboard path, a text check of the source file (contains `PastingFromClipboard`, `SelectedText = `, `LongPaste.IsLong`) like the WinUI ones.
  - See them fail, then implement.
- [ ] Implement the overlay, the three paste paths and the pane strip.
- [ ] Publish the Avalonia AOT head and drive it as P20-34 (same test texts, same steps). Popups are invisible to the capture tools (P20-19): the overlay is a panel in the window, not a popup, so it must be captured; if it is not, record `not driven (capture)`. Record the paste times next to the WinUI ones.
  - Captures in dark and light (French for one, English for the other): the popup over the grid, the pane strip. Read every PNG; captures are not committed. `Annuler` at the end.
- [ ] Update the README phase 20 section (status line of P20-31 ... P20-35) and add a line to `CHANGELOG.md` under the unreleased section for both fixes, if a changelog section for phase 20 exists (P20-28 wrote one).
- [ ] Close the app, `dotnet build-server shutdown`.

## Acceptance criteria
- Recipe 2 common acceptance of [verification-recipe.md](verification-recipe.md) with `Squill.slnx`, the Avalonia AOT publish (`eng/check-aot-warnings.ps1 -Head Avalonia`, baseline warnings only), the app starts, captures read (dark and light). Steps not driven are written `not driven (<why>)`.
- A paste of at least `LongPaste.Threshold` characters shows the `Pasting…` popup over the result area until it is done; shorter pastes behave as before; the pasted value is exactly the clipboard text.

## Risks
- `TextBox.PastingFromClipboard` and a settable `TextBox.SelectedText` exist in Avalonia 11; if Avalonia 12.1.6 removed or changed either, stop and report with a proposed card fix (do not hook `Ctrl+V` key events by hand).
- Same undo-stack check as P20-34.

## Suggested commit message
Show a Pasting popup with an indeterminate bar during a long paste in the Avalonia head
