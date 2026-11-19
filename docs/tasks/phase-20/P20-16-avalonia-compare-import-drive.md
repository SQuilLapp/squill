# P20-16 - Avalonia: drive schema compare (M09) and the import wizard (M10)

Status: todo
Depends on: P20-10

## Goal
`new-screens.md` rows 3 and 4 say `agent (Avalonia): not driven`; P19-11 drove only M01. Repeat P20-10 on the Avalonia head. Procedure and rules: recipe 3 of [verification-recipe.md](verification-recipe.md).

## Design decisions (final)
- Head: Avalonia AOT exe on Windows (scale 1.5 on this machine: pass `-Width 2160 -Height 1350` to `Capture-Window.ps1`, see P19-14), SQLite `-Scenario Full`, French light and dark, English dark.
- Read the Done notes of P20-10 first: same steps, same expected numbers (compare groups and counts of `scenario.sql`, the generated script, production confirmation on `cmp-target`, nothing left after running; import 37 rows in and rows 12, 25, 33 rejected), and reuse its fixes (a difference in shared Presentation code is already fixed; one in an Avalonia view is fixed here).
- The Avalonia import wizard picks the file through the platform storage provider (`AvaloniaDialogService.PickOpenFileAsync`): a native dialog; type the path with `Drive-Window.ps1 Type` after `Focus-Dialog`, or `not driven (file picker)`.
- Compare with `M09-*` and `M10-*`; fix small differences with tests (`tests/Squill.App.Avalonia.Tests/SchemaCompareUiTests.cs`, `ImportWizardUiTests.cs`); propose bigger ones.

## Out of scope
- Linux / macOS, M11 and M12 (P20-17).

## Tasks
- [ ] Drive, capture, read PNGs and composites; fixes; update the Avalonia `Result` cells of rows 3, 4, 7 and the Note of `new-screens.md` (never the ticks) and the Avalonia cells of `visual-spec.md` sections 16 and 19.
- [ ] Close everything.

## Acceptance criteria
- Recipe 2 common acceptance if code changed (`Squill.slnx`, Avalonia AOT publish, app starts, `LayoutOverlapTests` green); report lists compared / not driven per screen, theme and language.

## Risks
- Same as P20-10 (copy of `cmp_target.db`, file picker).

## Suggested commit message
Drive and compare the Avalonia schema compare and import wizard screens
