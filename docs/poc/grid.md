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

## WinUI head (P5-10)

`Controls/DataGridCanvas` (`CanvasControl.Draw` on Win2D, only the visible cells and columns are drawn, `CanvasTextLayout` cached in a 4,096-entry `LruCache`) over the same synthetic `IGridDataSource` (`SyntheticGridSource`, 1,000,000 rows x 12 columns). `GridLayout` is shared with the Avalonia head (`Squill.Presentation.Results`).

Run: `Squill.App.WinUI.exe --grid-bench` on the Native AOT publish (`dotnet publish src/Squill.App.WinUI -c Release -r win-x64`). 300 frames, 3 rows per frame, each `CanvasControl.Draw` schedules the next one; the numbers are printed to stdout (redirect it, the exe is a WinExe) and the app exits by itself.

| Metric | Value |
|---|---|
| Frames | 300 |
| avg frame ms (`Draw` callback) | 2.420 |
| p95 frame ms (`Draw` callback) | 3.400 |
| avg interval ms between frames | 16.617 |
| p95 interval ms between frames | 17.813 |

Budget: avg frame <= 16 ms. Verdict: met.

Same caveat as above: "frame ms" is the time in the `Draw` callback (recording the Win2D draw calls); the interval is capped by the 60 Hz display and GPU time is not included.
