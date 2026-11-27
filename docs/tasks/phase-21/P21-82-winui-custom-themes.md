# P21-82 - WinUI: apply custom themes at runtime

Status: todo
Depends on: P21-81

## Goal
The WinUI head applies a custom theme's colour overrides without a restart.

Exists today: generated `SquillXxxBrush` resources in theme dictionaries, `Tokens.Brush(this, key)` lookups and theme-change repaint in code-drawn controls (Win2D grid, ER, plan), `ThemeLookupScanTests`.

## Design decisions (final)
- On apply: write the override colours into the `Light` / `Dark` theme dictionaries' `SolidColorBrush.Color` (mutating the existing brushes so `{ThemeResource}` users update), then raise the same repaint path as a theme switch for Win2D canvases.
- Settings window: a `Theme` combo listing `Default` + theme files + presets; a preview chip row.

## Out of scope
- Avalonia (P21-83).

## Tasks
- [ ] Tests (repaint path called, brush colours changed); publish and drive (recipe 3): apply each preset and one file, dark and light, captures of the main window, grid and ER.

## Acceptance criteria
- Recipe 2 and 3 acceptance (WinUI).

## Risks
- Brushes frozen or shared by WinUI internals: if a mutation does not propagate, record it and fall back to "applied after restart" for that brush (Done notes).

## Open points (defaults)
- None.

## Suggested commit message
Apply custom themes at runtime in the WinUI head
