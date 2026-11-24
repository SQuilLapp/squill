# P20-32 - Avalonia: grid cells show a single-line, capped preview of long and multi-line values

Status: todo
Depends on: P20-31

## Goal
Bring the fix of P20-31 to the Avalonia head: a grid cell shows one line (line breaks and tabs flattened by `CellText.Preview`), clipped to the cell with an ellipsis, and a very long value is cut before it is measured or drawn. The full value stays what the cell holds, edits, previews and commits.

Files today: `src/Squill.App.Avalonia/Controls/DataGridCanvas.cs` (custom `Render`: cell drawing around line ~320, `Text(...)` around line ~860, `MeasureColumns` around line ~849).

## Root cause (same as P20-31, read in the code at `e3d27ea`)
- The cell draws `CellFormatter.FormatDisplay(...)` unchanged (line ~320), so a value with line breaks is a multi-line `FormattedText` (line ~872, no `MaxLineCount`, no `MaxTextWidth`, no trimming).
- It is centred on the row (`top + ((rowHeight - text.Height) / 2)`, line ~343): a tall text is drawn half above, half below its row.
- The only clip is per column, full height (`context.PushClip(new Rect(left, 0, colWidth, Bounds.Height))`, line ~267), so the text spills over the other rows and the header.
- The cache key `LruCache<(string Text, IBrush Brush, Face Face, bool Struck), FormattedText>` is the full string: every frame hashes the whole value, a miss shapes the whole value, and `MeasureColumns` (line ~849) shapes the full value of the first 100 rows.

## Design decisions (final)
- Reuse `CellText.Preview` (P20-31) unchanged; no new Presentation code.
- Cell drawing: after the placeholder / `NULL` choice, `value = CellText.Preview(value)`. `MeasureColumns`: `CellText.Preview(CellFormatter.FormatDisplay(...) ?? "NULL")`.
- **Ellipsis at the cell edge**: the cache key gets an `int Width` (0 = unbounded, used by headers, the gutter and measuring). When the unbounded text's `Width` is larger than `colWidth - (2 * CellPadding)`, draw a second `FormattedText` cached with `Width = (int)Math.Floor(colWidth - (2 * CellPadding))`, built with `MaxTextWidth = Width`, `MaxLineCount = 1` and `Trimming = TextTrimming.CharacterEllipsis`. Only overflowing cells get the second text; the numeric right alignment uses the width of the text drawn.
- No per-cell clip is added: a single line fits the row.
- Nothing changes in the cell editor `TextBox`, the preview pane, the clipboard copy, paste or the commit.

## Out of scope
- Long-paste speed and the progress popup (P20-33 ... P20-35), the WinUI head (P20-31), other places that show cell values.

## Tasks
- [ ] Tests first:
  - `tests/Squill.App.Avalonia.Tests/GridCellPreviewUiTests.cs` (headless, like `GridEditingUiTests.cs`): a grid over a source whose cell holds an indented JSON of 100 KB renders without throwing, and the `FormattedText` the canvas asks for is one line: expose nothing new for the test if a public way exists; otherwise a text check of `DataGridCanvas.cs` (contains `CellText.Preview(` at least twice, `MaxLineCount = 1`, `TextTrimming.CharacterEllipsis`), the same form as the WinUI text checks.
  - See them fail, then implement.
- [ ] Implement the changes in `DataGridCanvas.cs`.
- [ ] Publish the Avalonia AOT head and drive it like P20-31 (SQLite `-Scenario Full`, table `clients`, column `ville`, the same 200 KB indented JSON made in the scratchpad and put on the clipboard): paste through the in-place editor and through the preview pane, record what each stores; the cell shows one line with `…`; widen the column; the preview pane still shows the whole value; `Aperçu SQL` has the full value; `Annuler`.
  - Captures in dark and light (French for one, English for the other): the row with the long value, the widened column. Read every PNG; popups are invisible to the capture tools (P20-19), the grid is not a popup. Captures are not committed.
- [ ] Close the app, `dotnet build-server shutdown`.

## Acceptance criteria
- Recipe 2 common acceptance of [verification-recipe.md](verification-recipe.md) with `Squill.slnx`, the Avalonia AOT publish (`eng/check-aot-warnings.ps1 -Head Avalonia`, baseline warnings only), the app starts, captures read (dark and light). Steps not driven are written `not driven (<why>)`.
- A multi-line or very long value is drawn on one line inside its cell with an ellipsis; nothing is drawn outside its row; the value held, edited, previewed and committed is unchanged.

## Risks
- If `MaxLineCount` / `Trimming` on `FormattedText` do not behave as documented in Avalonia 12.1.6 (the text still wraps or no ellipsis shows), report it and keep the flattened, clipped text rather than drawing a hand-made ellipsis.
- Same data-loss check as P20-31 for the single-line in-place editor: report, do not fix here.

## Suggested commit message
Draw grid cells on one line with a capped preview in the Avalonia head
