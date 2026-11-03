# P10-17 - Completeness tests: no hard-coded user-visible string can come back

Status: todo
Depends on: P10-16

## Goal
Turn the inventory into a permanent guard: tests that fail when a user-visible literal appears outside the catalog or when a key is missing in a language.

## Design decisions (final)
- Reuse `HardcodedStringScanner` and `ignored-literals.txt` from P10-03; no second implementation. New file `tests/Squill.Presentation.Tests/i18n/EnforcementTests.cs` (Presentation.Tests already scans `src/`).
- `Scanner_FindsNoHardcodedUserText`: runs the scanner over every area and asserts the result is empty; the failure message lists `path:line kind "text"` and the instruction "add a catalog key (see docs/tasks/phase-10/migration-recipe.md) or, only for non-UI text, an entry with a reason in i18n/ignored-literals.txt".
- `XamlLiterals_AreBindingsOrCatalogLookups`: the XAML/AXAML part of the scanner alone, with its own failure message (separate test so that a view regression is named as such). Covers `Text`, `Content`, `Header`, `Title`, `Label`, `Watermark`, `PlaceholderText`, `ToolTip`, `ToolTip.Tip`, `ToolTipService.ToolTip`, `AutomationProperties.Name`, button-text attributes and element text.
- `IgnoredLiterals_AreStillPresent_AndHaveReasons`: every line of `ignored-literals.txt` has a non-empty reason, points to an existing file and its text still occurs in that file (stale entries fail), and the file is sorted.
- `UsedKeys_ExistInBothCatalogs`: extend the key scan of `UiTextTests` (`UiText.Get/Format/FormatPlural("..")`, `{loc:Text k}`, `loc:Loc.Key`, `Loc.ToolTipKey`, `Loc.HeaderKey`, `Loc.TitleKey`, `Loc.MenuTextKey`, `Loc.AutomationNameKey`) and assert each key exists in both catalogs; plural base keys count when `.one` and `.other` exist. Keys built dynamically are allowed only through a documented list `DynamicKeyPrefixes` in the test (`issue.` and any prefix found in the code, each with a comment); for each prefix the test asserts that every code of the matching `*MessageCodes.All` (P10-09) has `prefix + code` in both catalogs.
- `Catalog_HasNoUnusedKeys` is a warning-level test: it lists keys never referenced (not failing, printed through the test output) so that dead keys are visible; the implementer removes the dead keys found now.
- `TwoLanguageSmoke`: builds `SettingsViewModel`, `ConnectionEditorViewModel`, `TableDesignerViewModel` and `QueryTabViewModel` with test doubles under `Override(French)` then `Override(English)` and asserts that none of their user-visible string properties (at least `Title`, `StatusText`, the validation messages) is null, empty or of the form `!key!`, and that these properties differ between the two languages. Keep it small and data-driven.
- Both heads: a head-level test (Avalonia headless tests) that loads each main view under both languages and asserts that no `TextBlock.Text`/`Button.Content` in the visual tree contains `!`+`!`-marked missing keys. WinUI cannot be loaded headless: say so and rely on the XAML scan plus the published-exe check below.
- Document in `docs/tasks/README.md` conventions (one line): "User-visible text goes through `UiText`; see phase 10".

## Out of scope
- Translating anything new, a third language.

## Tasks
- [ ] Write the tests above; make them pass without weakening them (no wildcard ignore entries).
- [ ] Prove each guard works: temporarily add a literal `Text="Hello"` to a view and a `"Name is required."` literal to a view-model, confirm the tests fail with a useful message, remove them (the report says so).
- [ ] Run both head exes with `SQUILL_LANGUAGE=en` and `fr` and look for `!key!` markers on every main screen.

## Acceptance criteria
- `rtk bypass dotnet build Squill.Windows.slnx -c Release -warnaserror` exits 0 with 0 warnings; `rtk bypass dotnet test --solution Squill.Windows.slnx -c Release` is green.
- Both head publishes succeed with zero IL2xxx/IL3xxx warnings.
- No process left running.

## Suggested commit message
Add tests that enforce complete localization
