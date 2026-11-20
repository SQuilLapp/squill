# P20-22 - WinUI: the table designer name cell text is nearly invisible once edited (dark theme)

Status: todo
Depends on: P20-13

## Goal
Found by P20-13 on the published WinUI head (dark, French, SQLite): in `TableDesignerView` the name `TextBox` of a column row (`CellBoxStyle`, `src/Squill.App.WinUI/Views/TableDesignerView.xaml` near line 154) shows its text darker than the row background while it has focus, and the edited cell keeps the dim text after focus leaves it (`MODIFIEE` and `NOUVELLE` rows). Untouched cells and the explorer filter box (`SearchBox`) are readable. The typed name cannot be read, so the designer is hard to use in the dark theme.

## Design decisions (final)
- Reproduce first: designer of `clients`, `F2` on a row (or click the name), type, read the capture; try the `Defaut` cell and light theme too.
- Probable area: the `TextControlForeground*` lightweight-styling keys of `Styles/ControlResources.xaml` (all point to `SquillFgColor`, so check the focused / unfocused visual state resolution inside the `ItemsControl` data template, and whether `CellBoxStyle` (Background `Transparent`) breaks a state). Fix in the style (an explicit `Foreground` plus the state brushes in `CellBoxStyle.Resources`) rather than per cell; add a style test in `tests/Squill.App.WinUI.Tests/ControlStylesTests.cs` if a pattern fits.
- Re-drive with `Drive-Window.ps1`, dark and light, and read the PNG.

## Out of scope
- The not built designer features; Avalonia.

## Tasks
- [ ] Reproduce, fix, test, capture dark and light.

## Acceptance criteria
- Recipe 2 common acceptance with `Squill.Windows.slnx`; captures read.

## Suggested commit message
Keep the WinUI designer cell text readable while and after editing it
