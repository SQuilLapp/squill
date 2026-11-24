# P20-31 - WinUI: grid cells show a single-line, capped preview of long and multi-line values

Status: todo
Depends on: none

## Goal
Owner report after testing the WinUI AOT build: in a table with pending inserts (`4 modifications en attente, 4 insertions`, columns `Id uuid`, `Datatype text`, `GeoJson jsonb`), a long pretty-printed GeoJSON put in a `GeoJson` cell is drawn over many lines: the text of one cell spills above and below its row, over the other rows and the header, and the cell shows pieces such as `48.995338479487899 ], [ 2.63169...`. A grid cell must show **one line**: line breaks and tabs flattened, the text clipped to the cell with an ellipsis, very long values cut before they are measured or drawn. The full value stays what the cell holds, what the cell editor and the preview pane show, and what is committed. This card adds the shared rule (Presentation) and fixes the WinUI head; P20-32 fixes Avalonia.

Files today: `src/Squill.App.WinUI/Controls/DataGridCanvas.cs` (Win2D surface: `DrawColumn`, `TextLayout`, `MeasureColumns`), `src/Squill.Presentation/Results/CellFormatter.cs` (`FormatDisplay`), `src/Squill.Presentation/Results/CellPreviewViewModel.cs` (the pane beside the grid), `src/Squill.Presentation/Results/GridLayout.cs` (`MaxColumnWidth = 400`).

## Root cause (read in the code at `e3d27ea`)
1. `DrawColumn` (line ~320) draws `CellFormatter.FormatDisplay(...)`, which returns a stored `string` unchanged (`string s => s`), line breaks included. `CellValueParser` stores the text of a `text` / `jsonb` cell as typed (no JSON normalization), so a pasted indented JSON keeps its `\n` and indentation. The preview pane can also produce one: `CellPreviewViewModel.Refresh` sets `Text` to the **pretty-printed** JSON (`TryPretty`) and `Apply` commits `Text`, so an edit applied from the pane stores the indented form.
2. `TextLayout` (line ~476) builds a `CanvasTextFormat { WordWrapping = NoWrap }`: that only stops soft wrapping; DirectWrite still starts a new line at every hard break. The requested height `(float)_layout.RowHeight` does not clip anything.
3. The text is centred on the row (`top + ((rowHeight - LayoutBounds.Height) / 2)`, line ~344): an N-line layout gets a negative offset and is drawn centred on its row, half above, half below.
4. The only clip is the column layer `ds.CreateLayer(1f, new Rect(left, 0, colWidth, height))` (line ~296): full canvas height, so nothing keeps the text inside its row; the backgrounds of later rows paint over parts of it (the overlapping look of the screenshot).
5. Cost: the layout cache key is the full string (`LruCache<(string Text, Face Face, bool Struck), CanvasTextLayout>`), so every frame hashes the whole value for every visible cell, a cache miss lays out the whole value, and `MeasureColumns` (line ~550) lays out the full value of each of the first 100 rows. A multi-megabyte value costs that on every redraw.

## Design decisions (final)
- **Shared rule** (Presentation, no UI type): new `src/Squill.Presentation/Results/CellText.cs`, `public static class CellText` with XML docs:
  - `public const int PreviewLength = 500;` (a 400 px column shows about 55 mono characters; 500 leaves room for a column the user widens and never measures more).
  - `public static string Preview(string text)`: one line, at most `PreviewLength` characters.
    - A maximal run of characters that are `char.IsWhiteSpace(c) || char.IsControl(c)` becomes **one space** when the run contains anything other than U+0020 (so `\r\n`, `\n`, `\r`, `\t`, U+0085, U+2028, U+2029, other control characters and the indentation after a line break all collapse to one space; an indented JSON reads like minified JSON with spaces). A run of plain spaces is kept as is.
    - When the flattened text would be longer than `PreviewLength`, keep the first `PreviewLength - 1` characters and append `…` (U+2026); never cut between a high and a low surrogate (cut one character earlier).
    - Fast path, no allocation: when `text.Length <= PreviewLength` and `text.AsSpan().IndexOfAny(s_flattened) < 0` (a `SearchValues<char>` of U+0000 ... U+001F, U+007F, U+0085, U+2028, U+2029), return the **same instance**.
    - Bounded work: build into a `stackalloc`/pooled buffer of `PreviewLength + 1` chars and stop reading the input as soon as the output is full and one more character is known, or after `8 * PreviewLength` input characters (a run of a million line breaks must not be scanned to its end); stopping early before the end of the input means the ellipsis.
  - Display only: never used for editing, the clipboard (`ToTsv` keeps `CellFormatter.Format`), export, the preview pane or the commit.
- **WinUI `DataGridCanvas`**:
  - `DrawColumn`: after the placeholder / `NULL` choice, `value = CellText.Preview(value)`; the layout is built from the preview, so the cache key is bounded too.
  - `MeasureColumns`: `CellText.Preview(CellFormatter.FormatDisplay(...) ?? "NULL")`.
  - **Ellipsis at the cell edge**: the cache key becomes `(string Text, Face Face, bool Struck, int Width)`, `Width = 0` meaning unbounded (header texts, gutter, measuring keep 0). In `DrawColumn`, get the unbounded layout first; when its `LayoutBounds.Width` is larger than `colWidth - (2 * CellPadding)`, take the layout for `Width = (int)Math.Floor(colWidth - (2 * CellPadding))` instead, built with `format.TrimmingGranularity = CanvasTextTrimmingGranularity.Character` and `format.TrimmingSign = CanvasTrimmingSign.Ellipsis` and that width as the layout width. Only overflowing cells get a second layout; a column drag creates new trimmed layouts for the visible overflowing cells only, and the LRU (4096) disposes the old ones. The numeric right alignment keeps using the width of the layout drawn.
  - No per-cell clip layer is added: a single line fits the row.
- Nothing changes in the cell editor `TextBox` (`DataGridView._editor`), the preview pane, `CellFormatter`, `GridEditController.Paste` (tab-separated paste semantics stay) or what is committed.

## Out of scope
- The Avalonia head (P20-32). Long-paste speed and a progress popup (P20-33 ... P20-35). Whether an applied JSON is stored indented or minified (open question for the owner, see the README phase 20 section). The column menu value list, the explorer and any other place that shows cell values (they use `TextBlock`s with their own trimming).

## Tasks
- [ ] Tests first:
  - `tests/Squill.Presentation.Tests/Results/CellTextTests.cs`: `Preview_ShortPlainText_ReturnsSameInstance`; `Preview_PlainSpaces_Kept` (`"a   b"` same instance); `Preview_LineBreaks_BecomeOneSpace` (`"a\r\nb\nc\rd"` gives `"a b c d"`); `Preview_IndentedJson_ReadsAsOneLine` (`"{\n  \"type\": \"Point\",\n  \"coordinates\": [\n    2.63,\n    48.99\n  ]\n}"` gives `"{ \"type\": \"Point\", \"coordinates\": [ 2.63, 48.99 ] }"`); `Preview_TabsAndControlChars_BecomeOneSpace`; `Preview_ExactlyPreviewLength_NoEllipsis`; `Preview_LongText_CappedWithEllipsis` (length `PreviewLength`, last char `…`); `Preview_SurrogatePairAtCut_NotSplit`; `Preview_MillionLineBreaksThenText_StopsEarly` (result length at most `PreviewLength`, ends with `…`); `Preview_TenMegabytes_IsCapped`.
  - `tests/Squill.App.WinUI.Tests/GridCellPreviewTests.cs` (text checks; link `Controls\DataGridCanvas.cs` as `Content` `Xaml\DataGridCanvas.cs` in the csproj like `ExplorerFilterMenu.cs`): the file contains `CellText.Preview(` at least twice (draw and measure), `CanvasTrimmingSign.Ellipsis` and `CanvasTextTrimmingGranularity.Character`, and still `WordWrapping = CanvasWordWrapping.NoWrap`.
  - See them fail, then implement.
- [ ] Implement `CellText` and the `DataGridCanvas` changes.
- [ ] Publish the WinUI AOT head and drive it (P20-01 `Drive-Window.ps1`, recipe 3 of [verification-recipe.md](verification-recipe.md)), SQLite `-Scenario Full`, table `clients` (`ville` is a nullable `TEXT`):
  - make a test value in the scratchpad (not committed): an indented JSON of about 200 KB (for example a GeoJSON `FeatureCollection` of a few thousand coordinate pairs, written by a PowerShell one-liner) and put it on the clipboard with `Set-Clipboard -Value (Get-Content -Raw <file>)`;
  - add a row (`Ctrl+Insert`), set `raison_sociale` and `cree_le`, then put the JSON in `ville` both ways and **record in the Done notes what each way stores**: (a) double-click the cell, `Ctrl+V` in the in-place editor, `Enter` (does the single-line WinUI `TextBox` keep the line breaks?); (b) select the cell, paste into the preview pane, `Appliquer`;
  - the cell shows one line, `{ "type": "FeatureCollection", ...` cut with `…` at the cell edge; no text outside the row; widen the column by drag: more text, still one line; the grid scrolls and redraws without visible lag (note `LastRenderMs` if the bench overlay is used, otherwise by eye);
  - the preview pane still shows the whole value; the in-place editor opened on the cell still holds the whole value (check its length through UI Automation `Value` if the driver can read it, otherwise `not driven`); `Aperçu SQL` shows the full value in the `INSERT`; `Annuler` drops it (do not commit unless the scenario is a throw-away copy).
  - Captures in dark and light (`SQUILL_LANGUAGE` French for one, English for the other): (1) the row with the long value, (2) the widened column. Read every PNG. Captures are not committed.
- [ ] Close the app, `dotnet build-server shutdown`.

## Acceptance criteria
- Recipe 2 common acceptance with `Squill.Windows.slnx` (the Presentation type is built by both solutions; `Squill.slnx` must stay green too), the WinUI AOT publish (`eng/check-aot-warnings.ps1 -Head WinUI`, baseline warnings only), the app starts, captures read (dark and light). Steps that cannot be driven are written `not driven (<why>)` in the Done notes, never claimed.
- A multi-line or very long value is drawn on one line inside its cell with an ellipsis; no text is drawn outside its row; the value held, edited, previewed and committed is unchanged.

## Risks
- `CanvasTrimmingSign` / `TrimmingGranularity` are Win2D 1.4 `CanvasTextFormat` properties; if trimming does not show the ellipsis with a fixed layout width, report it and keep the clipped text (the column layer already clips horizontally) rather than drawing a hand-made ellipsis.
- If path (a) of the drive shows the in-place editor **drops** the line breaks of a value that has them (open the editor on a multi-line value, `Enter` without typing, value changed), that is data loss: report it in the Done notes and propose a card (do not fix it here).

## Suggested commit message
Draw grid cells on one line with a capped preview in the WinUI head
