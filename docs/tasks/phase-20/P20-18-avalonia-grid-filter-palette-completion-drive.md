# P20-18 - Avalonia: finish the grid filter, command palette and completion popup passes

Status: todo
Depends on: P20-11, P20-12

## Goal
P19-15 drove on Avalonia (dark, French) the filter popup, chip, column menu, one palette query and the completion popup after `select * from cl`; it did not reach the light theme, English, filter steps 3-4, 7, 9-14, palette steps 18-27, or the completion join and snippet rows. Procedure and rules: recipe 3 of [verification-recipe.md](verification-recipe.md).

## Design decisions (final)
- Head: Avalonia AOT exe, SQLite `-Scenario Full`, French light and dark, English light; scale 1.5 pixel sizes as in P20-16.
- Read the Done notes of P20-11 and P20-12 first; reuse their findings and fixes. Drive the same rows of `grid-filter-palette.md` (grid 3-14 and palette 15-27 except those P19-15 already read, which only need a light-theme and English pass) and the rows of `completion.md` (popup keys, documentation panel, join row with `ON`, snippet tab stops) in the Avalonia head. The completion popup here is AvaloniaEdit's (ADR 0002), a different widget from LoomEditor's: record any behavioral difference between the two popups under the Notes of `completion.md` (it asks for it); the single-entry popup that stayed open on WinUI (P20-12) is checked here too.
- Compare with `M05-Grille-*` and `M13-Palette-*` light and dark; small fixes with tests (`tests/Squill.App.Avalonia.Tests/FilterBarUiTests.cs`, `CommandPaletteUiTests.cs`, `AvaloniaSqlEditorTests.cs`); propose bigger ones.

## Out of scope
- WinUI, Linux / macOS, Narrator row (stays `not driven (no screen reader)`).

## Tasks
- [ ] Drive, capture, read PNGs; fixes; update the Avalonia `Result` cells of `grid-filter-palette.md` and `completion.md` (never ticks) and the Avalonia cells of `visual-spec.md` rows M05 and M13.
- [ ] Close everything.

## Acceptance criteria
- Recipe 2 common acceptance if code changed (`Squill.slnx`, Avalonia AOT publish, app starts, `LayoutOverlapTests` green); report lists per step `ok` / `bug` / `not driven (why)`.

## Risks
- Largest Avalonia matrix: stop at about 50 % context and record `not driven (time)` rather than stretch a claim.

## Suggested commit message
Drive the Avalonia grid filter, command palette and completion popup in both themes and English
