# P20-02 - WinUI: the remaining brush lookups follow the chosen theme, with a guard test

Status: done
Depends on: none

## Goal
P19-20 made the workspace follow `settings.json` `theme: Light` while Windows is dark by replacing the static `Brush(key)` helpers with `Tokens.Brush(key)` (`src/Squill.App.WinUI/Views/Tokens.cs`: reads the variant of `Tokens.Active`). It added no test and found the sites by reading code. A scan finds token brushes still read through `Application.Current.Resources[...]`, which follows the **system** theme:
- `Controls/SearchBox.cs`: `SquillFg3Brush`, `SquillBgRaisedBrush`, `SquillFocusBrush`, `SquillBorderBrush` (`SyncBrushes`)
- `Controls/CommandPalette.cs` lines near 280 and 320: `SquillEnv{Suffix}Brush`, `SquillFg3Brush`
- `Controls/DataGridView.cs` near 188: `SquillDangerBrush` / `SquillAccentBrush` (cell editor frame)

The search box (grid filter, palette), the palette icons and the cell editor frame therefore keep the wrong variant in a light theme chosen over a dark system.

## Design decisions (final)
- First check each key: a brush that is **not** defined per theme (the environment colors `SquillEnv*Brush` may be theme independent: read `src/Squill.App.WinUI/SquillTokens.xaml` / `squill.tokens.json`) is left alone and listed in the Done notes; only theme-dependent keys move to `Tokens.Brush(key)`.
- The guard test is a source scan, in the style of `tests/Squill.Presentation.Tests/i18n/HardcodedStringScanner.cs`: `tests/Squill.App.WinUI.Tests/ThemeLookupScanTests.cs` walks up to the folder holding `Squill.slnx`, reads every `*.cs` under `src/Squill.App.WinUI` and fails on a line matching `Application.Current.Resources[` followed by a string (or interpolated string) ending in `Brush"` / `Color"`, except files and keys on an explicit allow list inside the test (theme-independent keys, with a one-line reason each; `Tokens.cs` itself). Test name `Token_brushes_are_read_through_Tokens`.
- Fix the sites with `Tokens.Brush(key)`; where the owner element is at hand and already themed, `Tokens.Brush(element, key)`. Controls that cache a brush (`SearchBox._frame`, the palette icon) must be repainted when the theme changes: find how `QueryTabView.PaintResults` is triggered (P19-20) and hook the same `ActualThemeChanged` / `Tokens.Active` change, do not add a second mechanism.
- Add `Tokens_variant_follows_active_theme` to the same test file if a pure helper can be split out (`Tokens.VariantOf(ElementTheme)` returning `"Light"` or `"Default"`, used by both `Brush` and `Color`); skip it if the split is more code than the assertion is worth.

## Out of scope
- `Application.Current.Resources["...Style"]` lookups (styles resolve their `ThemeResource` brushes per element), fonts and sizes.
- Capturing the result (P20-15).

## Tasks
- [x] Write `ThemeLookupScanTests`; run it; it fails on the sites above (paste the failing lines in the Done notes).
- [x] Fix the sites; the scan passes.
- [ ] Publish the WinUI AOT head, start it with a Light `settings.json` while Windows is dark, open the grid filter bar and `Ctrl+K`: the search box frame, icons and cell editor frame are light-themed (read a capture; if it cannot be driven, say so, P20-15 repeats it).

## Acceptance criteria
- Recipe 2 common acceptance with `Squill.Windows.slnx`; the new test fails before and passes after.

## Risks
- Allow-listing too much makes the test worthless: every allow-list entry needs its reason and a check in `SquillTokens.xaml` that the key has no `Light` variant.

## Suggested commit message
Read the remaining WinUI token brushes through the chosen theme and guard them with a test

## Done notes

- Test `Token_brushes_are_read_through_Tokens` (regex `Resources[...Brush"|Color"...]`, case-insensitive, no allow-listed keys: `Tokens.cs` only) failed on exactly these lines before the fix:
  - `Controls/CommandPalette.cs:280` `SquillEnv{Suffix}Brush`; `:320` `SquillFg3Brush`
  - `Controls/DataGridView.cs:188` `SquillDangerBrush` / `SquillAccentBrush`
  - `Controls/SearchBox.cs:25, 71, 72` `SquillFg3Brush`, `SquillBgRaisedBrush`, `SquillFocusBrush` / `SquillBorderBrush`
  - `Views/HistoryView.xaml.cs:27` `SquillSuccessBrush` / `SquillDangerBrush` (not in the card list; found by the scan)
- Every key has a `Light` variant in `Styles/SquillTokens.xaml`, including the `SquillEnv*` colors, so nothing was left alone. Element-owned sites use `Tokens.Brush(this, key)`; `CommandPalette` icon creation and `HistoryView.StatusBrush` are static and use `Tokens.Brush(key)` (the palette icons are repainted by `PaintStatic` on a theme change). `SearchBox` keeps its glyph in a field and repaints it in `SyncBrushes`, which already runs on `ActualThemeChanged`.
- `Tokens_variant_follows_active_theme` skipped: splitting `VariantOf` costs more than the assertion is worth.
- Light-theme drive of the filter bar, `Ctrl+K` and the cell editor frame: not driven (left to P20-15). The AOT publish of the WinUI head passed `eng/check-aot-warnings.ps1 -Head WinUI` (baseline only).
- `ExplorerAndTabsTests.Explorer_tree_rows_are_24_high` failed with and without this change (pre-existing, unrelated: PR 43 raised the row height to 28); it now expects 28 and is renamed `Explorer_tree_rows_are_28_high`; everything else is green.
