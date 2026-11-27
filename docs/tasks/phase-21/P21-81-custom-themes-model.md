# P21-81 - Design system, Platform and Presentation: custom themes (token overrides)

Status: todo
Depends on: none

## Goal
Users can pick a custom theme (accent colour and full colour overrides) on top of the built-in light and dark themes.

Exists today: `ThemeMode` (`System`, `Light`, `Dark`) in `AppSettings`; design tokens `tokens/squill.tokens.json` generated at build time into XAML/AXAML by `Squill.DesignSystem` (`SquillXxxBrush` resources); both heads repaint on theme change (P20-02, P20-42 guards).

## Design decisions (final)
- A theme file `themes/<name>.json` in the user data folder: `base` (`light` | `dark`), `colors` (token name -> `#RRGGBB[AA]`, only colour tokens; unknown names reported), validated by a pure `ThemeFile` parser (source-generated JSON) with a contrast check (text on surface >= 4.5:1, warning only).
- Built-in presets shipped as embedded theme files: `Accent: blue / green / orange / purple` (accent token overrides only).
- `AppSettings.CustomTheme` (name or null); `ThemeMode` unchanged; Settings lists the theme files, `Import...` copies a file in, `Open themes folder`.
- The token list for overrides comes from `Squill.DesignSystem` (so a theme cannot name a non-colour token).

## Out of scope
- Applying the overrides at runtime (P21-82, P21-83), fonts and sizes, a visual theme editor.

## Tasks
- [ ] Tests: parse, unknown token, invalid colour, contrast warning, presets load, settings round trip.

## Acceptance criteria
- Recipe 2 common acceptance.

## Risks
- None specific.

## Open points (defaults)
- Owner: a theme editor with colour pickers (default no: files only).

## Suggested commit message
Add custom theme files with colour token overrides
