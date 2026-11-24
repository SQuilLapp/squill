# P20-34 - WinUI: a "Pasting..." popup with an indeterminate progress bar during a long paste

Status: done
Depends on: P20-33, P20-40

Note (P20-40): the in-place editor already handles `Paste` (and `PreviewKeyDown` while read-only) since P20-40. For the editor, do not add a second handler: wrap P20-40's paste body with the overlay for a long text (show, yield, same body, hide). The `SelectedText = text` design below stays for the preview pane only.

## Goal
When the user pastes a text of at least `LongPaste.Threshold` characters (P20-33) into the grid, the in-place cell editor or the preview pane, show a modal-ish popup over the result area, an indeterminate progress bar and `Pasting…` / `Collage en cours…`, like DataGrip; it closes when the paste is done. The window keeps repainting meanwhile (the managed work runs off the UI thread since P20-33; the native `TextBox` insert stays on the UI thread and the bar keeps moving because WinUI runs the indeterminate animation on the composition thread). Below the threshold nothing changes.

Files today: `src/Squill.App.WinUI/Views/QueryTabView.cs` (`body` grid of row 4 holds `_grid`, the preview splitter and `_preview`), `src/Squill.App.WinUI/Controls/DataGridCanvas.cs` (`PasteAsync`, `ApplyEditKey`), `src/Squill.App.WinUI/Controls/DataGridView.cs` (`_editor`), `src/Squill.App.WinUI/Views/CellPreviewPanel.cs` (`_text`), precedents `src/Squill.App.WinUI/Views/ColumnMenu.cs` (an indeterminate `ProgressBar` while values load) and the `ProgressBar` style of `Styles/Controls.xaml`.

## Design decisions (final)
- **No existing busy overlay or progress dialog exists** (grep of `ProgressBar`, `ProgressRing`, `IsBusy`, `Loading` in the heads and Presentation: only inline bars in `ColumnMenu`, `QueryTabView._progress`, the import wizard, the update banner and window). A `ContentDialog` is not used: it would be a second modal over the window for a sub-second wait and cannot be shown from inside a native paste. Build one small control.
- New `src/Squill.App.WinUI/Views/BusyOverlay.cs`, `internal sealed partial class BusyOverlay : Grid` (one-line `<summary>`), built in code like `ResizeGrip`: `Visibility.Collapsed` by default; background a scrim (`SquillBgBaseBrush` at `Opacity` 0.6 on a child `Border`, so the pointer is caught and the grid is dimmed); centred card `Border` (`SquillBgOverlayBrush`, `SquillBorderBrush`, `CornerRadius` from `OverlayCornerRadius`, padding 16) holding a `StackPanel` (spacing 10) with a `TextBlock` (`SquillFgBrush`) and a `ProgressBar { IsIndeterminate = true, Width = 220 }` (the existing implicit style). Brushes through `Tokens.Brush(this, key)` painted on `Loaded` and `ActualThemeChanged` (`ThemeLookupScanTests`). `AutomationProperties.Name` and the `TextBlock` = `UiText.Get("results.pasting")`; `AutomationProperties.LiveSetting = Assertive`. Methods `Show()` / `Hide()`; `Show` also sets a counter so nested shows hide only on the last `Hide`.
- New keys (fr and en catalogs, parity and used-keys tests): `results.pasting` = `Pasting…` / `Collage en cours…`; `preview.working` = `Checking the value…` / `Vérification de la valeur…`.
- **Placement**: one `BusyOverlay` per `QueryTabView`, added last to `body` with `Grid.SetColumnSpan(overlay, 3)`, so it covers the grid and the preview pane, not the editor or the tool bars.
- **Grid `Ctrl+V`** (`DataGridCanvas.PasteAsync`): after `GetTextAsync`, `LongPaste.IsLong(text)` -> raise new `public event EventHandler<bool>? BusyChanged` (`true`), `await edit.PasteAsync(text, CancellationToken.None)`, then `BusyChanged(false)` in a `finally`; short texts keep the synchronous `edit.Paste`. `DataGridView` forwards the event; `QueryTabView` shows / hides the overlay. While busy, `DataGridCanvas.OnKeyDown` and `OnCharacterReceived` return at once (a `_busy` flag), so keys do not edit the grid under the popup.
- **In-place editor and preview pane** (`TextBox.Paste` event, raised for `Ctrl+V`, `Shift+Insert` and the context menu): handle every paste: `e.Handled = true` synchronously, then `var text = await Clipboard.GetContent().GetTextAsync()` (only when the content has `StandardDataFormats.Text`); short text -> `box.SelectedText = text` (inserts at the caret like the built-in paste). Long text -> show the overlay, `await Task.Delay(1)` so it is laid out and rendered, `box.SelectedText = text`, then hide. `TextChanged` flows as today: the editor's `SetText` is cheap (P20-33), the pane's view-model validates in the background. Both text boxes raise the request through an event (`DataGridView.BusyChanged`, new `CellPreviewPanel.BusyChanged`) so only `QueryTabView` touches the overlay.
- **Pane working strip**: `CellPreviewPanel` shows a 2 px indeterminate `ProgressBar` (like `QueryTabView._progress`) under its header while `CellPreviewViewModel.IsWorking`, with `AutomationProperties.Name` = `preview.working`; `Appliquer` follows `ApplyCommand.CanExecute` as today. The `Pasting…` popup is not used for this (selecting a long cell is not a paste).
- No `ItemsSource` (Native AOT trap, P20-27), no `Application.Current.Resources[...]` brush lookups.

## Out of scope
- The Avalonia head (P20-35), cancelling a paste, a determinate percentage, pasting into the SQL editor (`LoomEditor`) or the import wizard, the first-load pretty-printing of a long cell selected without pasting (shown by the pane strip only).

## Tasks
- [x] Tests first:
  - `tests/Squill.App.WinUI.Tests/LongPasteUiTests.cs` (text checks; link `Views\BusyOverlay.cs`, `Views\CellPreviewPanel.cs` and `Controls\DataGridView.cs` as `Content` in the csproj like `ExplorerFilterMenu.cs`, `Controls\DataGridCanvas.cs` is linked by P20-31): `BusyOverlay.cs` contains `IsIndeterminate = true`, `results.pasting`, `Tokens.Brush` and neither `ItemsSource` nor `Application.Current.Resources`; `DataGridCanvas.cs` contains `LongPaste.IsLong` and `PasteAsync(`; `DataGridView.cs` and `CellPreviewPanel.cs` contain `.Paste +=` and `SelectedText = `; `CellPreviewPanel.cs` contains `IsWorking` and `preview.working`.
  - Catalog parity and used-keys tests green with the two keys.
  - See them fail, then implement.
- [x] Implement the overlay, the keys, the three paste paths and the pane strip.
- [x] Publish the WinUI AOT head and drive it (P20-01 `Drive-Window.ps1`, recipe 3 of [verification-recipe.md](verification-recipe.md)), SQLite `-Scenario Full`, table `clients`, column `ville`; test texts made in the scratchpad (not committed) and put on the clipboard with `Set-Clipboard`: an indented JSON of 50 KB (below the threshold), 1 MB and 5 MB, and a tab-separated block of 2,000 rows x 3 columns (about 150 KB) for the grid path:
  - in-place editor, 50 KB: no popup; 1 MB and 5 MB: the popup shows at once, the bar moves (two captures about one second apart differ in the bar), it closes after the insert; `Enter` commits; the grid stays one line (P20-31);
  - preview pane, 1 MB: popup, then the pane strip while it validates, `Appliquer` disabled until it ends, enabled after for valid JSON; an invalid JSON (delete one `}`) shows the error after the strip ends; typing one character in the 1 MB text: no visible freeze;
  - grid `Ctrl+V` of the tab-separated block on a table with enough added rows (or note the rows it stopped at): popup, then the cells are set; one `Changed` (the edit bar count updates once);
  - the window repaints and can be moved while the popup is up (record what was seen: the bar moving, the window dragged); keys pressed during the popup do not reach the grid;
  - record in the Done notes the time from `Ctrl+V` to the popup closing for each size, next to the P20-33 table.
  - Captures in dark and light (`SQUILL_LANGUAGE` French for one, English for the other): the popup over the grid, the pane strip. Read every PNG; captures are not committed. `Annuler` the pending changes at the end.
- [x] Close the app, `dotnet build-server shutdown`.

## Acceptance criteria
- Recipe 2 common acceptance with `Squill.Windows.slnx` (`Squill.slnx` green too), the WinUI AOT publish (`eng/check-aot-warnings.ps1 -Head WinUI`, baseline warnings only), the app starts, captures read (dark and light). Steps not driven are written `not driven (<why>)`.
- A paste of at least `LongPaste.Threshold` characters shows the `Pasting…` popup with a moving indeterminate bar over the result area until it is done; shorter pastes behave as before; the pasted value is exactly the clipboard text (check the length in the preview pane or `Aperçu SQL`).

## Risks
- Handling `TextBox.Paste` replaces the built-in paste: `SelectedText` must insert at the caret, replace a selection and stay on the undo stack (`Ctrl+Z` after a paste); check it in the drive, and if undo is lost report it in the Done notes (acceptable, not a blocker).
- If the bar does not move while the native insert runs (the composition-thread animation assumption is wrong), keep the popup (it still tells the user what happens) and record it; do not split the insert into chunks.
- If `Task.Delay(1)` is not enough for the overlay to be rendered before the insert (popup never seen), use `DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, ...)` for the insert instead and say so.
- The in-place editor is a single-line `TextBox`: P20-31 records whether it keeps line breaks; this card does not change that.

## Done notes
- Implemented: `BusyOverlay`, keys `results.pasting` / `preview.working`, `DataGridCanvas.BusyChanged` (`_busy` blocks keys, long `Ctrl+V` through `PasteAsync`), `DataGridView.BusyChanged` (wraps P20-40's `PasteIntoEditorAsync`: show, `Task.Delay(1)`, same body, hide), `CellPreviewPanel` `Paste` handler + `BusyChanged` + 2 px working strip, overlay in `QueryTabView.body` (column span 3). `LongPasteUiTests` added (tests first, 176 WinUI tests green, 4503 passed in the Windows solution).
- Build `Squill.Windows.slnx` and `Squill.slnx`: 0 warnings, 0 errors. WinUI AOT publish: baseline warnings only; the published exe starts and stays alive 8 s.
- Not driven (context budget, session cap): the interactive drive of recipe 3 (popup captures dark/light, bar movement, timings for 50 KB / 1 MB / 5 MB, undo after paste, preview strip, keys during the popup, window drag). Do it in a manual pass.
- Note for P20-41: the acceptance line "the pasted value is exactly the clipboard text" is literal only for the text in the box; P20-41 normalizes line breaks to `
` when Apply stores the value, so the stored value can differ from the clipboard text in `
` cases. Not changed here.

## Suggested commit message
Show a Pasting popup with an indeterminate bar during a long paste in the WinUI head
