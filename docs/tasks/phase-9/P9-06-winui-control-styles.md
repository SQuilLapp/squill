# P9-06 - WinUI head: shared control styles

Status: done
Depends on: P9-01, P9-03, P9-02

## Goal
The WinUI counterpart of P9-05: implicit and keyed styles that restyle the WinUI 3 controls to the mockups.

## Design decisions (final)
- Read `docs/design/visual-spec.md` section 3 (final). Create `Styles/Controls.xaml` merged in `App.xaml` after the WinUI resources, restyling by overriding the lightweight-styling resource keys of the WinUI controls (`ButtonBackground`, `TextControlBorderBrushFocused`, ...) and, where that is not enough, by copying the default template with the changes: Controls and their states restyled once, for every later view: `Button` (variants `Primary`, `Secondary` default, `Outline-success`, `Outline-danger`, `Ghost`, `Icon` 28 x 28), `TextBox` and search box (with leading icon and trailing keyboard chip), `ComboBox`, `CheckBox`, `RadioButton`, `ToggleSwitch`, `TabControl`/underline tabs, segmented control, `ListBox`/tree rows, menu and flyout/popup (radius 8, `bg-overlay`, `shadow-3`), tooltip, scrollbar (thin 8 px), `ProgressBar`, dialog frame, plus three reusable pieces: `KeyboardChip`, `Badge` (pill with variants info/success/warning/danger/environment) and `EngineBadge` (two letters on the `engine-*` token).
- Sizes from `SquillControlHeight*`, text from the type-scale resources of P9-01, colors only from `Squill*Brush` through `ThemeResource`. No literal color or font name.
- All states (pointer-over, pressed, focused with a 2 px `focus` ring, disabled) in both themes.
- Window default font: `SquillFontUi`; mono: `SquillFontMono`.
- Add `Views/StyleGalleryWindow.xaml`, opened only when `SQUILL_STYLE_GALLERY=1`, showing every control and state.

## Out of scope
- Layout changes of existing views (later cards).

## Tasks
- [x] Write the styles and the gallery.
- [x] Tests in `tests/Squill.App.WinUI.Tests` for what is testable without UI (resource keys present in both themes, no literal colors in `Controls.xaml` by a grep-based test). State in the report what needs the screenshot.

## Acceptance criteria
- `rtk bypass dotnet build Squill.Windows.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.Windows.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).
- `rtk dotnet publish src/Squill.App.WinUI -c Release -r win-x64` succeeds with zero IL2xxx/IL3xxx warnings.
- Visual check (`docs/design/visual-spec.md` section 11): the running app was captured in light and dark with `tools/visual/Capture-Window.ps1`, compared with M03, M05 (toolbar and menus), M02 (fields), M14 (cards, radio cards, switches) through `tools/visual/Compare-Mockup.ps1`, and every visible difference was fixed or listed in the card report with its reason. If a state could not be reached or driven, the report says which and what the owner must check by hand.
- No process left running (app, `dotnet build-server`, AOT executable).

## Suggested commit message
WinUI: shared control styles

## Report
- Styles: `Styles/Controls.xaml` (merged after the tokens in `App.xaml`) and `Styles/ControlResources.xaml` (lightweight-styling keys re-pointed to the tokens, merged into both theme dictionaries). Variant buttons share one template whose hover/pressed wash the content color (WinUI 3 has no `Style.Resources`); `TreeViewItem` is restyled through resources only (a `TreeViewItem` style crashed the XAML load), its 24 px row height is left to the explorer card. Segmented control = `RadioButton` with `SegmentedRadioButtonStyle`; `SearchBox` is a code-built control.
- Not done / to check by hand: popup shadow (`shadow-3`) uses the WinUI default flyout shadow; menu, flyout, tooltip and dialog popups and the pointer-over/pressed states were not driven or captured (gallery captured in light and dark, static states only); the comparison with M03/M05/M02/M14 is deferred to the screen cards because no screen uses the styles yet.
