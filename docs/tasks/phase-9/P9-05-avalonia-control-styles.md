# P9-05 - Avalonia head: shared control styles

Status: done
Depends on: P9-01, P9-03, P9-02

## Goal
One set of styles for the controls every Avalonia view uses, matching the mockups, so view cards only compose them.

## Design decisions (final)
- Read `docs/design/visual-spec.md` section 3 (final). Create `Styles/Controls.axaml` (merged after `FluentTheme` in `App.axaml`; selectors override the Fluent templates through `ControlTheme`/style classes, not by editing Fluent) covering: Controls and their states restyled once, for every later view: `Button` (variants `Primary`, `Secondary` default, `Outline-success`, `Outline-danger`, `Ghost`, `Icon` 28 x 28), `TextBox` and search box (with leading icon and trailing keyboard chip), `ComboBox`, `CheckBox`, `RadioButton`, `ToggleSwitch`, `TabControl`/underline tabs, segmented control, `ListBox`/tree rows, menu and flyout/popup (radius 8, `bg-overlay`, `shadow-3`), tooltip, scrollbar (thin 8 px), `ProgressBar`, dialog frame, plus three reusable pieces: `KeyboardChip`, `Badge` (pill with variants info/success/warning/danger/environment) and `EngineBadge` (two letters on the `engine-*` token).
- Sizes come from the density resources (`SquillControlHeight*`); text sizes from the type-scale resources of P9-01; every color from `Squill*Brush`; radii from `SquillRadius*`. No literal color or font name in the file.
- Pointer-over, pressed, focused (2 px `focus` ring), disabled and keyboard-focus states for every control, both themes.
- Set the window default `FontFamily`/`FontSize` from `SquillFontUi` / body size in `App.axaml`, and `SquillFontMono` for the editor, grid and every `.mono` class.
- Add `Views/StyleGallery.axaml` (a window opened only when the environment variable `SQUILL_STYLE_GALLERY=1` is set at startup, otherwise never reachable) showing every control and state, used for the screenshots of this card.

## Out of scope
- Applying the styles to existing views beyond what happens automatically through implicit styles; layout changes (later cards).

## Tasks
- [x] Write the styles and the gallery.
- [x] Headless tests in `tests/Squill.App.Avalonia.Tests`: gallery loads in both themes without binding errors; a primary button resolves `SquillAccentBrush` background; a disabled button resolves `SquillFgDisabledBrush`.

## Acceptance criteria
- `rtk bypass dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).
- `rtk dotnet publish src/Squill.App.Avalonia -c Release -r win-x64` succeeds with zero IL2xxx/IL3xxx warnings.
- Visual check (`docs/design/visual-spec.md` section 11): the running app was captured in light and dark with `tools/visual/Capture-Window.ps1`, compared with M03, M05 (toolbar and menus), M02 (fields), M14 (cards, radio cards, switches) through `tools/visual/Compare-Mockup.ps1`, and every visible difference was fixed or listed in the card report with its reason. If a state could not be reached or driven, the report says which and what the owner must check by hand.
- No process left running (app, `dotnet build-server`, AOT executable).

## Suggested commit message
Avalonia: shared control styles
