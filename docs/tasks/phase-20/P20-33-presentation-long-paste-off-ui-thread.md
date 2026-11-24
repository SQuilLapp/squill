# P20-33 - Presentation: a long paste does its heavy work off the UI thread

Status: done
Depends on: P20-31, P20-40

Note (P20-40): path E below is described as it was at `e3d27ea`. Since P20-40, a multi-line paste into the in-place editor goes to `GridEditController.PasteText` and the box only shows a 500-character read-only preview; measure path E with that code (E1 is then the single-line case).

## Goal
Owner request after the report of P20-31: pasting a very long text into a cell freezes the window; show a "Pasting..." popup with an indeterminate progress bar, like DataGrip, and keep the UI responsive. This card measures where a long paste really spends its time once P20-31 removed the drawing cost, defines the threshold, and moves the managed work that can leave the UI thread off it. The popup itself is built per head: WinUI P20-34, Avalonia P20-35.

Files today: `src/Squill.Presentation/Results/CellPreviewViewModel.cs`, `src/Squill.Presentation/Editing/GridEditController.cs` (`SetText`, `Commit`, `Paste`), `src/Squill.Presentation/Editing/ResultEditorViewModel.cs` (`TrySetCell`, `Refresh`), `src/Squill.Presentation/Results/EditableGridDataSource.cs` (`SetCell` raises `Changed` once per cell), `src/Squill.Presentation/Results/CellValueParser.cs`.

## Where a long paste spends its time (read in the code at `e3d27ea`)
Three paste paths, for a text of N characters:
- **E, the in-place cell editor** (`DataGridView._editor`, a single-line `TextBox`): (E1) the native insert into the `TextBox`, UI thread, cannot be moved; (E2) `TextChanged` reads `_editor.Text` (N characters marshalled) and calls `GridEditController.SetText`: `Sanitize` (one pass, identity for text and JSON) and `Validate` -> `CellValueParser.TryParse` (a `string` column returns the text; cheap), then `SyncEditor` compares the texts again: all O(N), **cheap, stays as is**; (E3) `Enter` -> `Commit` -> `TrySetCell` -> `SetCell` -> `Changed` -> `ResultEditorViewModel.Refresh` (`ChangeSetBuilder.Build`, O(rows), no SQL text: cheap) **and** `CellPreviewViewModel.OnGridChanged` -> `Refresh`: `TryPretty` (full `JsonDocument.Parse`, indented write, UTF-8 decode) **then** `Text = pretty` -> `OnTextChanged` -> `Validate` -> `TryPretty` **a second time** plus `CellValueParser.TryParse`, then the pane's multi-line `TextBox` receives the pretty text (native, UI thread); `EditMove.Down` then moves the selection and the pane refreshes for the next cell. (E4) the grid redraw laid out all N characters per frame: fixed by P20-31.
- **P, the preview pane** (`CellPreviewPanel._text`, `AcceptsReturn = true`): (P1) the native insert; (P2) `TextChanged` -> `Text` -> `OnTextChanged` -> `Validate`: `TryPretty` (parse and serialize the whole document) and `CellValueParser.TryParse`, **on every later keystroke too**, so typing in a large JSON lags.
- **G, `Ctrl+V` on the grid** (no editor open): the clipboard read is already async; `GridEditController.Paste` splits lines and tabs and calls `TrySetCell` per cell, and each `SetCell` raises `Changed`, so every cell runs `ChangeSetBuilder.Build` over all rows, a preview refresh and a redraw: **O(cells x rows) on the UI thread** for a large tab-separated paste. (A multi-line JSON pasted this way is split into rows, tab-separated semantics; unchanged here.)

Conclusion: after P20-31, the managed costs worth moving are the preview pane's JSON work (`TryPretty` twice per refresh, once per keystroke) and the per-cell `Changed` storm of path G. The native `TextBox` inserts stay on the UI thread (the heads show the popup around them, P20-34 / P20-35).

## Design decisions (final)
- **Measure first** (task 1). Add a temporary `Stopwatch` + `Debug.WriteLine` around E1 ... E3, P1, P2 and G in the WinUI head (not committed), publish AOT, paste indented JSON of 100 KB, 1 MB and 5 MB through E, P and G (`clients.ville`, SQLite `-Scenario Full`), and write the table (stage x size, ms) in the Done notes. If a stage this card does not touch dominates (for example E1 or the pane's native `TextBox` set), say so in the Done notes; the popup of P20-34 still covers it. If the numbers contradict the list above (a stage named cheap costs more than 100 ms at 1 MB), stop and report with a proposed card fix.
- **Threshold**: new `src/Squill.Presentation/Editing/LongPaste.cs`, `public static class LongPaste` with `public const int Threshold = 100_000;` and `public static bool IsLong(string? text) => text is not null && text.Length >= Threshold;`. Measured as `string.Length` of the clipboard text (UTF-16 code units, read once after the async clipboard read; no byte encoding): about 100 KB of ASCII JSON. Below it, every path stays synchronous and shows nothing. If the measurement shows every path under 200 ms at 1 MB, set `Threshold = 1_000_000` and say so.
- **Preview pane** (`CellPreviewViewModel`):
  - Parse once: `Refresh` keeps the result of its `TryPretty`; when the value is valid JSON, `Validate` does not parse it again (split `Validate` so the JSON part is skipped when already known valid).
  - For a text with `LongPaste.IsLong(text)`, `Refresh` and `OnTextChanged` run the JSON work (`TryPretty`, `Validate`) through `Task.Run`, latest request wins (a `CancellationTokenSource` replaced on each request, a result applied only when its request is still the current one), resumed with `ConfigureAwait(true)` like `ImportWizardViewModel`. New `[ObservableProperty] public partial bool IsWorking { get; private set; }` is `true` meanwhile; `CanApply` adds `!IsWorking`; `ShowFormat` stays, `Format` does nothing while working. In `Refresh`, `Text` is set **once**, when the pretty text is ready (the pane's native `TextBox` is not filled twice). For tests: `internal Task Pending { get; }`, the last background request (completed when idle).
- **Grid paste in one change** (path G):
  - `EditableGridDataSource.SetCells(IReadOnlyList<(long Row, int Column, object? Value)> cells)`: the body of `SetCell` moves to a private `SetCellCore` without the event; `SetCell` = core + `Changed`; `SetCells` = core per cell + **one** `Changed` (none for an empty list). Same exceptions as `SetCell`.
  - `ResultEditorViewModel.TrySetCells(...)`: the checks of `TrySetCell` (`IsViewActive`, `CanEdit`, read-only, deleted row) per cell on the UI thread, then one `SetCells`.
  - `GridEditController.PasteAsync(string text, CancellationToken cancellationToken)` returns `Task<string?>` (same error text as `Paste`): (1) on the caller's thread, map the text's lines and tabs to target cells (row, column, `ColumnInfo`, skip read-only columns, stop at the grid edges: today's `Paste` rules); (2) `Task.Run`: `CellValueParser.TryParse` per cell (pure, no grid access), stopping at the first error; (3) `ConfigureAwait(true)`, then one `TrySetCells` with the cells before the first error, and the first error as `results.pasteIssue`. `Paste(string)` is rewritten on the same three steps run synchronously (one `Changed` instead of one per cell), so both share the code and the existing `Paste_*` tests stay green unchanged.
  - Splitting stays line / tab based; `\N` is NULL; nothing else changes in the semantics.
- No change to `GridEditController.SetText` / `Commit` (path E is cheap in managed code) or to `CellValueParser`.

## Out of scope
- The popup and the head wiring (P20-34 WinUI, P20-35 Avalonia), and therefore the localization keys (added by P20-34, used by both heads). Pasting a multi-line text into one cell from the grid (today it is split into rows; owner question), minifying JSON on apply, cancelling a paste.

## Tasks
- [x] Measure (see design decisions), record the table in the Done notes, remove the temporary code.
- [x] Tests first:
  - `tests/Squill.Presentation.Tests/Results/EditableGridDataSourceTests.cs` (existing file or new): `SetCells_RaisesChangedOnce`; `SetCells_Empty_RaisesNothing`; `SetCells_DeletedRow_Throws` (same as `SetCell`).
  - `tests/Squill.Presentation.Tests/Editing/GridEditControllerTests.cs`: `Paste_RaisesGridChangedOnce` (a 3 x 2 block); `PasteAsync_WritesBlockFromActiveCell` (same data as `Paste_WritesBlockFromActiveCell`); `PasteAsync_ReportsFirstErrorAndKeepsEarlierCells`; `PasteAsync_LongText_ParsesOffTheCallingThread` (a 200,000-character single cell: the parse runs on another thread, checked through a column whose parse is observable, or by asserting the returned task is not completed synchronously; pick the form that is not timing-dependent).
  - `tests/Squill.Presentation.Tests/Query/CellPreviewTests.cs`: `Refresh_ValidJson_ParsesOnce` (through a counter or by checking `Error` is null and `Text` is indented for a document; pick a form that does not need a hook in production code, otherwise an `internal` parse counter is acceptable); `Text_LongJson_ValidatesInBackground` (`IsWorking` true right after setting a 150,000-character `Text`, false after `await Pending`, `Error` then set for invalid JSON, `ApplyCommand.CanExecute` false while working); `Refresh_LongJson_SetsTextOnce` (count `PropertyChanged` for `Text`); `Text_ShortJson_StaysSynchronous` (`IsWorking` never true).
  - `tests/Squill.Presentation.Tests/Editing/LongPasteTests.cs`: `IsLong_BelowThreshold_False`, `IsLong_AtThreshold_True`, `IsLong_Null_False`.
  - See them fail, then implement.
- [x] Implement `LongPaste`, `SetCells` / `TrySetCells`, `PasteAsync` and the shared `Paste` steps, the preview view-model changes.

## Acceptance criteria
- Recipe 2 common acceptance of [verification-recipe.md](verification-recipe.md) with `Squill.slnx` and `Squill.Windows.slnx` (both build Presentation); no head code is committed by this card, so no AOT publish beyond the measurement one.
- A grid paste raises `Changed` once; the preview pane parses a JSON value once per refresh and, above the threshold, off the UI thread with `IsWorking` while it runs; the measurement table is in the Done notes.

## Risks
- `EditableGridDataSource` is not thread-safe: the background step of `PasteAsync` must not read the grid (only the text and the `ColumnInfo`s captured on the caller's thread).
- Tests without a synchronization context resume on the thread pool after `ConfigureAwait(true)`; tests must `await` the returned task / `Pending`, never sleep.
- The two known timing tests of the solution may be re-run alone once (recipe 2); a new test of this card must not be timing-dependent.

## Suggested commit message
Move the heavy work of a long paste off the UI thread and raise one change per grid paste

## Done notes
- **Measurement** (temporary test, removed; JIT build, `Release`, warm, indented-JSON array of objects, 100 KB / 1 MB / 5 MB of input):

  | Stage (managed) | 100 KB | 1 MB | 5 MB |
  |---|---|---|---|
  | `TryPretty` (parse + indented write + decode) | 13 ms | 39 ms | 197 ms |
  | `CellValueParser.TryParse` of a `string` column | 1 ms | 0 ms | 0 ms |

  The native stages (E1 `TextBox` insert, the pane's `TextBox` set, E4 redraw) and the end-to-end WinUI AOT paste through E, P and G were **not measured**: no GUI drive was available in this session. Nothing contradicts the card: the managed stages named cheap stay under 100 ms at 1 MB. The 5 MB `TryPretty` (about 200 ms, done twice per refresh before this card) is what moves off the UI thread.
- **Threshold**: kept at `LongPaste.Threshold = 100_000`, not raised to 1,000,000, because the native `TextBox` stages that the popup of P20-34 / P20-35 covers are unmeasured. P20-34 should record the native timings when it drives the WinUI AOT build and may then raise the constant.
- Deviation: the "latest request wins" rule uses an integer request counter instead of a `CancellationTokenSource` (same behaviour; the superseded result is dropped, the work itself is not cancelled). `PasteAsync` does take a token and cancels the parse.
- `EditableGridDataSource.SetCells` raises `Changed` once, also if a later cell throws (earlier cells are already recorded). `ResultEditorViewModel.TrySetCells` is `internal`; a refused cell (view active, read-only, deleted row) is reported like a parse error, with the `results.pasteIssue` prefix.
- `CellPreviewViewModel`: `Refresh` parses once (`Validate` skips the JSON parse when already known valid, and `Text` is assigned with `OnTextChanged` muted, so Validate also runs once). `Refresh_ValidJson_ParsesOnce` is tested through its observable result (`Error` null, indented `Text`) without a parse counter in production code. Long-text tests use a queueing `SynchronizationContext` so the in-between state is deterministic.
