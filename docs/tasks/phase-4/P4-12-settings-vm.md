# P4-12 - Presentation: settings view-model

Status: done
Depends on: P4-09

## Goal
Expose preferences to the UI and apply them live.

## Design decisions (final)
- `SettingsViewModel(ISettingsStore)`: `Theme` (`ThemeMode`), `EditorFontSize`, `MaxGridRows`, `FormatterKeywordCase`; loaded on `LoadAsync`, saved (debounce-free) on every change after load; `ThemeChanged` event for the head to switch the XAML theme variant; `FormatCommand` helper on `QueryTabViewModel` (`FormatDocumentCommand`) calls `SqlFormatter.Format` with the settings and replaces the editor text keeping the caret near its previous relative position.
- Query tabs read `MaxGridRows` at run time from the same settings instance (adapt P4-07 to take the settings view-model's current value through `ISettingsStore.LoadAsync` cached by the view-model).

## Out of scope
- Settings window UI (P5-07).

## Tasks
- [x] Implement the view-model and `FormatDocumentCommand`.
- [x] Tests: load/save round trip with the in-memory store, clamping reaches the UI, format command output equals `SqlFormatter` output.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add settings view-model and format command
