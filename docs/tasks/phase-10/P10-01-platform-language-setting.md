# P10-01 - Platform: language setting

Status: todo
Depends on: none

## Goal
Store the user's language choice (System default / Français / English) in the settings file.

## Design decisions (final)
- New public enum `AppLanguage { System, French, English }` in `src/Squill.Platform/Settings/AppLanguage.cs`, XML-documented like `ThemeMode`.
- `AppSettings` gets a fifth positional parameter `AppLanguage Language = AppLanguage.System` (default value so that settings files written before phase 10 still load). `AppSettings.Default` uses `AppLanguage.System`. `Clamp` is unchanged.
- The enum is serialized as a string by the existing `SettingsJsonContext` (`UseStringEnumConverter`); property name `language`.
- `System` means: the language of the operating-system UI when it is French (`fr`, any region) or English, English in every other case. This resolution is NOT done in Platform (it needs `CultureInfo` and the catalog); it is P10-04.
- A settings file with an unknown `language` value must behave like any other unreadable value of the store today (check `JsonSettingsStore.LoadAsync`: it must return `AppSettings.Default`, not throw); add a test that pins whatever it does.
- Update every `new AppSettings(` call in `src/` and `tests/` (grep), including `SettingsViewModel.Current` (pass `AppLanguage.System` for now; P10-04 wires the property).

## Out of scope
- Resolving `System`, the view-model property, any UI (P10-04 to P10-06).

## Tasks
- [ ] Add `AppLanguage`, extend `AppSettings`, fix all construction sites.
- [ ] Tests in `tests/Squill.Platform.Tests`: round-trip of each value, a legacy JSON file without `language` loads as `System`, an unknown value falls back as decided above, `Default.Language` is `System`.

## Acceptance criteria
- `rtk bypass dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings (use `Squill.Windows.slnx` when the WinUI head has a call site).
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green.
- No process left running.

## Suggested commit message
Add the language setting to the settings store
