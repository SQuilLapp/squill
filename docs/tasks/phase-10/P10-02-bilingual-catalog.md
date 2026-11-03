# P10-02 - Presentation: bilingual string catalog

Status: todo
Depends on: P10-01

## Goal
Turn the French-only catalog into two catalogs (French and English) with identical keys, a runtime language, plural handling, and tests that make a missing or empty translation impossible.

## Design decisions (final)
- Public enum `UiLanguage { French, English }` in `Squill.Presentation.Localization`.
- Replace `UiTextCatalog` by two internal classes, `UiTextCatalogFr` and `UiTextCatalogEn`, each with `internal static readonly (string Key, string Value)[] Entries`, same key order, same section comments. `UiTextCatalogFr` keeps the existing 234 entries unchanged. `UiTextCatalogEn` has the English text of every one of them (natural English, sentence case, the same placeholders and punctuation style; menu and column-header words capitalised like the French). Do not rename any key. Language names are the same in both catalogs: `settings.language.french` = `Français`, `settings.language.english` = `English`.
- `UiText` keeps its public API (`Get`, `Format`) and gains: `UiLanguage Language { get; }` (default `French` until `Initialize` runs, so the existing tests keep passing), `CultureInfo Culture { get; }` (`fr-FR` for French, `en-US` for English), `static void Initialize(UiLanguage language)` (called once at startup by P10-04; replaces the language and rebuilds nothing: both tables are built once as `FrozenDictionary`), `static string FormatPlural(string baseKey, int count, params object?[] args)` and `static IDisposable Override(UiLanguage language)`.
- `Override` stores the language in an `AsyncLocal<UiLanguage?>` that takes precedence over `Initialize`; disposing restores the previous value. It exists for tests (parallel-safe, flows through `await`) and is documented as such. No other code may call it.
- **Missing key:** `Get`/`Format` return `!key!` as today (a test fails on it, see below); a key present in French but absent in English must never exist (parity test).
- **Placeholders:** `{0}`, `{1}` .NET composite format, formatted with `UiText.Culture`; a literal brace is `{{`. Each key has the same set of placeholder indexes in both languages (tested). Never concatenate catalog strings.
- **Plurals:** a pair `<baseKey>.one` / `<baseKey>.other` per countable message. `FormatPlural` chooses `.one` when the category is "one" and passes `count` as `{0}` followed by `args` as `{1}`...: French "one" is `count` 0 or 1 (absolute value below 2), English "one" is exactly 1. Existing callers that pick `.one`/`.other` by hand (grep `\.one`) are switched to `FormatPlural`. Only these two categories exist; no other language is planned.
- **Not translated** and not in the catalog: database engine and driver messages, SQL, identifiers, user data (see `docs/tasks/phase-10/migration-recipe.md` rule 5).
- Parity and quality tests (Presentation.Tests, `Localization/CatalogTests.cs`): both catalogs have exactly the same key set; keys are unique in each; no value is null, empty or whitespace; placeholder indexes match per key; every `.one` has a `.other` and vice versa in both catalogs; no English value equals its French value unless the key is on an explicit allow-list inside the test (proper names, `SQL`, `OK`-like words: each entry commented); every key used in `src/` exists (the existing `UiTextTests` scan, now also matching `UiText.FormatPlural("...")`).
- Migrate `UiTextTests` expectations that assume `fr-FR` globally to `Override(UiLanguage.French)` and add English counterparts of the format and plural tests.

## Out of scope
- Reading the setting and calling `Initialize` (P10-04), culture of other formatters (P10-07), any new string.

## Tasks
- [ ] Split the catalog, write the 234 English values, extend `UiText`.
- [ ] Parity, placeholder, plural and `Override` tests.
- [ ] Update `docs/architecture.md` only if it describes the catalog (one line).

## Acceptance criteria
- `rtk bypass dotnet build Squill.Windows.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.Windows.slnx -c Release` is green.
- Both head publishes (`rtk dotnet publish <head project> -c Release -r win-x64`) succeed with zero IL2xxx/IL3xxx warnings.
- No process left running.

## Suggested commit message
Add the English string catalog and the runtime language of UiText
