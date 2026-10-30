# Grid virtualization benchmark

Risk R4: the result grid must stay smooth on 1,000,000 rows.

## Avalonia head (P5-02)

`DataGridCanvas` (custom `Render`, only the visible cells and columns are drawn, `FormattedText` cached in a 4,096-entry LRU) over a synthetic `IGridDataSource` of 1,000,000 rows x 12 columns (ints, strings, dates, 10% NULL).

Run: `Squill.App.Avalonia.exe --grid-bench` on the Native AOT publish (`dotnet publish src/Squill.App.Avalonia -c Release -r win-x64`). The window scrolls 300 frames by 3 rows from `TopLevel.RequestAnimationFrame`, prints the numbers to stdout and exits by itself.

| Metric | Value |
|---|---|
| Frames | 300 |
| avg frame ms (`Render` on the UI thread) | 4.389 |
| p95 frame ms (`Render` on the UI thread) | 13.161 |
| avg interval ms between frames | 16.448 |
| p95 interval ms between frames | 25.277 |

Budget: avg frame <= 16 ms. Verdict: met.

"frame ms" is the time spent in `DataGridCanvas.Render` (recording the draw calls on the UI thread); the interval figures are the wall-clock time between animation frames and are capped by the display refresh rate (60 Hz here). GPU rasterization time is not included.
