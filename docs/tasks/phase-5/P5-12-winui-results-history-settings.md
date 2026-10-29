# P5-12 - WinUI head: results panel, history, snippets and settings

Status: todo
Depends on: P5-11

## Goal
The WinUI equivalent of P5-06 and P5-07.

## Design decisions (final)
- Reuse `GridSelection` (move it to `Squill.Presentation.Results` with its tests, adapting the Avalonia head); Win2D grid gets selection rendering, keyboard navigation, TSV copy, header-click column selection.
- Results tab strip, running/empty states, export via `FileSavePicker` initialized with the window handle (`WinRT.Interop.WindowNative.GetWindowHandle`), `PickSaveFileAsync` implemented accordingly.
- Sidebar segmented switch Explorer/History/Snippets, `SettingsWindow` or `ContentDialog` (Ctrl+,) as in P5-07.

## Out of scope
- LoomEditor (P5-13).

## Tasks
- [ ] Implement the pieces listed.
- [ ] Run the AOT exe: select cells, copy, export CSV, change theme; report the observed behaviour.

## Acceptance criteria
- `rtk dotnet build Squill.Windows.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.Windows.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).
- `rtk dotnet publish src/Squill.App.WinUI -c Release -r win-x64` succeeds with zero IL2xxx/IL3xxx warnings.

## Suggested commit message
Add WinUI results panel, history, snippets and settings
