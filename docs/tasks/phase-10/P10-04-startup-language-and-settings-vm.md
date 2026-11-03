# P10-04 - Presentation: language at startup and in the settings view-model

Status: todo
Depends on: P10-01, P10-02

## Goal
Resolve the language once at startup (before any view-model or window exists), apply it to `UiText` and the .NET cultures, and expose the choice in `SettingsViewModel`.

## Design decisions (final)
- **Switching takes effect after a restart, not live.** Reason: every string is resolved once, at XAML load (`{loc:Text}`, `Loc.Key`) or when a view-model builds its text; live switching would need change notification on every binding, every computed view-model string and every code-behind assignment in both heads, which is large and fragile. A restart needs no code beyond a notice. The settings window shows a note while the chosen language differs from the active one (P10-05/06).
- `UiLanguage` resolution in `LanguageResolver.Resolve(AppLanguage setting, CultureInfo osUiCulture)` (public static, Presentation): `French` -> French, `English` -> English, `System` -> French when `osUiCulture.TwoLetterISOLanguageName` is `fr`, English otherwise (including every other language and the invariant culture). Pure function, unit-tested with `fr-FR`, `fr-BE`, `en-GB`, `de-DE`, invariant.
- `LanguageSetup.Apply(ISettingsStore store, string? environmentOverride, CultureInfo osUiCulture)` (public static, Presentation/Localization) returns the resolved `UiLanguage` and: loads the settings synchronously (`store.LoadAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult()`, documented: once, at startup, before the UI thread runs, tiny JSON file); applies `environmentOverride` when it is `fr`, `en` or `system` (case-insensitive; the `SQUILL_LANGUAGE` variable, never saved, used by tests and captures; any other value is ignored); calls `UiText.Initialize`; sets `CultureInfo.DefaultThreadCurrentCulture`, `DefaultThreadCurrentUICulture`, `CurrentCulture` and `CurrentUICulture` of the calling thread to `UiText.Culture`.
- `AppComposition.Create` calls `LanguageSetup.Apply(settingsStore, Environment.GetEnvironmentVariable("SQUILL_LANGUAGE"), CultureInfo.CurrentUICulture)` first, before it builds any view-model, in both heads. Verify that both heads build their services through `AppComposition.Create`; if one does not, call `Apply` as the first statement of its startup instead. Say which in the report.
- `SettingsViewModel` gets: `AppLanguage Language` (observable, saved like the other settings, included in `Current` and `LoadAsync`), `UiLanguage ActiveLanguage` (the value of `UiText.Language` when the view-model was created), `bool RestartRequired` (`LanguageResolver.Resolve(Language, CultureInfo.CurrentUICulture)` differs from `ActiveLanguage`; raises `PropertyChanged` when `Language` changes) and `IReadOnlyList<LanguageOption> LanguageOptions` (`record LanguageOption(AppLanguage Value, string Label)`; labels `settings.language.system` (translated), `settings.language.french`, `settings.language.english`). Add the three keys plus `settings.language` (row label) and `settings.restartNeeded` ("Redémarrez SQuilL pour appliquer la langue." / "Restart SQuilL to apply the language.") to both catalogs.
- With `SQUILL_LANGUAGE` set, `RestartRequired` still compares against the saved choice only (the override is a test aid; document it in a comment).

## Out of scope
- The language row in the settings windows (P10-05, P10-06), formatting of numbers and dates (P10-07), migrating any string.

## Tasks
- [ ] `LanguageResolver`, `LanguageSetup`, `AppComposition` call, view-model members, catalog keys.
- [ ] Tests in `tests/Squill.Presentation.Tests`: resolver table, `Apply` with a fake `ISettingsStore` (each `AppLanguage`, env override valid and invalid), cultures set, `RestartRequired` true/false/after load, language saved and reloaded through the view-model.
- [ ] Document `SQUILL_LANGUAGE` in `tools/visual/README.md` and the project README (one line each).

## Acceptance criteria
- `rtk bypass dotnet build Squill.Windows.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.Windows.slnx -c Release` is green.
- Both head publishes succeed with zero IL2xxx/IL3xxx warnings; launching each exe with `SQUILL_LANGUAGE=en` and a throwaway `SQUILL_DATA_DIR` shows the English text of the strings already in the catalog (menu bar, explorer title) and `fr` shows French.
- No process left running.

## Suggested commit message
Apply the language at startup and expose it in the settings view-model
