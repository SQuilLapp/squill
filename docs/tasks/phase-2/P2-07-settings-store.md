# P2-07 - Platform: application settings store

Status: done
Depends on: P2-06

## Goal
Persist user preferences consumed by the view-models.

## Design decisions (final)
- `sealed record AppSettings(ThemeMode Theme, double EditorFontSize, int MaxGridRows, KeywordCase FormatterKeywordCase)` with `AppSettings.Default` = (`System`, 13, 1_000_000, `Upper`); `enum ThemeMode { System, Light, Dark }`; `enum KeywordCase { Upper, Lower, Preserve }` (`KeywordCase` lives in `src/Squill.Core/Text/KeywordCase.cs` because the SQL formatter, P3-14, also uses it).
- `interface ISettingsStore { ValueTask<AppSettings> LoadAsync(CancellationToken); ValueTask SaveAsync(AppSettings, CancellationToken); }`, `JsonSettingsStore` on `settings.json` via the helper of P2-06 and a source-generated context. Values are clamped on load: font size 8-32, `MaxGridRows` 1,000-10,000,000.

## Out of scope
- UI (P4-12).

## Tasks
- [x] Implement the types, store and clamping.
- [x] Tests: defaults when missing, round trip, clamping, corrupt file => defaults.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add application settings store
