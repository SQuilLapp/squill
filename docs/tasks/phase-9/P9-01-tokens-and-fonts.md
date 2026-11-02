# P9-01 - Design system: font, type scale and missing tokens, bundled fonts

Status: todo
Depends on: none

## Goal
Make the design tokens and the font files complete enough for every later card: fonts the mockups use, typography, spacing and shadow tokens as resources in both heads, and the few colors the mockups use that have no token.

## Design decisions (final)
- Read `docs/design/visual-spec.md` sections 2 and 3 first; they are final.
- Fonts: add `assets/fonts/` with the **OFL** font files and their license text: `JetBrainsMono` (Regular, Italic, SemiBold, Bold), `InterTight` (SemiBold, Bold), `Inter` (Regular, SemiBold, Italic, as fallback UI font). Download them from the official releases (JetBrains/JetBrainsMono and rsms/inter on GitHub; Inter Tight from the Google Fonts repository folder `ofl/intertight`). If a download is impossible, stop with a report.
- Avalonia: `AvaloniaResource` entries linking the files into `Assets/Fonts/`, and `FontFamily` resources `SquillFontUi`, `SquillFontMono`, `SquillFontBrand` in a new `Styles/SquillFonts.axaml` (merged in `App.axaml`) using `avares://Squill.App.Avalonia/Assets/Fonts#JetBrains Mono` style URIs. WinUI: `Content` entries (`CopyToOutputDirectory`) and `FontFamily` resources of the same names using `ms-appx:///Assets/Fonts/<file>#<family>`, in `Styles/SquillFonts.xaml`. Verify that the font really loads (a TextBlock rendering with it) in both heads; Win2D text formats (`CanvasTextFormat.FontFamily`) must use the same URI form and be verified in the grid.
- Token generator (`src/Squill.DesignSystem/XamlGenerator.cs`, run with `tools/Squill.TokenGen`; regenerate and commit `src/Squill.App.Avalonia/Styles/SquillTokens.axaml` and `src/Squill.App.WinUI/Styles/SquillTokens.xaml`) also emits: the `typescale` entries as `SquillFontSize<Name>` and `SquillLineHeight<Name>` (double) and `SquillFontWeight<Name>`; the `space` entries as `SquillSpace<n>` (double); the `shadow-2`/`shadow-3` entries only where the platform has an equivalent resource type (otherwise skip them and say so in the report). The font families are not generated: they are the `SquillFont*` resources of the previous point, whose fallback list is the token's `font.*` list.
- New color tokens in `tokens/squill.tokens.json` (light and dark values sampled with a pixel reader from the PNGs, not guessed; record the sampled coordinates in the card report): `env-prod-soft`, `env-staging-soft`, `env-dev-soft`, `env-local-soft` (the PROD/STAGING/DEV/LOCAL pill backgrounds of M03/M01), `engine-postgres`, `engine-mysql`, `engine-sqlserver`, `engine-sqlite` (the engine badge fills of M02, white text on all), `kbd-bg`, `kbd-border` if the keyboard chips of M03 use a color no existing token matches (otherwise reuse `bg-raised`/`border` and do not add them).
- Existing token values stay unless a sampled mockup color differs by more than 6 per channel; then the token is corrected and the report lists old and new value.
- The existing `Squill.DesignSystem.Tests` pattern checks generation; extend it for the new groups and the new colors (light and dark present, every referenced resource exists in both generated files).

## Out of scope
- Control styles (P9-05, P9-06), localization, any view.
- Bundling Segoe UI Variable (system font).

## Tasks
- [ ] Add the font files and licenses, register them in both heads, verify rendering.
- [ ] Extend the generator, regenerate both resource files, add the color tokens.
- [ ] Tests in `tests/Squill.DesignSystem.Tests`.

## Acceptance criteria
- `rtk bypass dotnet build Squill.Windows.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.Windows.slnx -c Release` is green.
- `rtk dotnet publish src/Squill.App.Avalonia -c Release -r win-x64` and `rtk dotnet publish src/Squill.App.WinUI -c Release -r win-x64` succeed with zero IL2xxx/IL3xxx warnings.
- A screenshot of each head (any window) shows JetBrains Mono rendered in the grid or editor, or the report states how the font loading was verified otherwise.
- No process left running (app, `dotnet build-server`, AOT executable).

## Suggested commit message
Add bundled fonts, type scale and missing design tokens
