# P5-11 - WinUI head: workspace shell and connections

Status: todo
Depends on: P5-10

## Goal
The WinUI equivalent of P5-04 and P5-05.

## Design decisions (final)
- Same layout and behaviours as P5-04 (sidebar, tabs, editor/results split, status bar, shortcuts via `KeyboardAccelerator`s) built with WinUI controls (`TreeView`, `TabView`, `Grid` + `GridSplitter`-like splitter from `CommunityToolkit.WinUI.Controls.Sizers` only if it restores under AOT, otherwise a small custom splitter) and `x:Bind` compiled bindings with `Mode=OneWay` where needed.
- Connection editor as a `ContentDialog` bound to `ConnectionEditorViewModel`, list interactions as in P5-05 (`MenuFlyout`, double-tap connects). Results show the Win2D grid for grid results and a `TextBlock` for messages (tab strip is P5-12).
- Honor `SQUILL_DATA_DIR` as in P5-04. Theme from `SettingsViewModel.Theme` via `FrameworkElement.RequestedTheme`.

## Out of scope
- Selection/copy/export, history/snippets/settings (P5-12).

## Tasks
- [ ] Implement views, dialogs and shortcuts.
- [ ] Run the AOT exe against a SQLite profile, run `select 1`, describe/screenshot the result in the report, close the app.

## Acceptance criteria
- `rtk dotnet build Squill.Windows.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.Windows.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).
- `rtk dotnet publish src/Squill.App.WinUI -c Release -r win-x64` succeeds with zero IL2xxx/IL3xxx warnings.
- Report includes evidence of a query result displayed.
- No process left running.

## Suggested commit message
Add WinUI workspace shell and connection dialog
