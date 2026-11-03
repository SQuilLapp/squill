# ADR 0007 - Localization (French and English)

- Status: accepted
- Date: 2026-11-03

## Context
Phase 9 shipped the interface in French only ([ADR 0006](0006-visual-conformance.md)). Phase 10 (cards P10-01 to P10-18) makes both heads (WinUI 3 and Avalonia) bilingual French / English under Native AOT, without reflection and without changing features. Card details are in [docs/tasks/phase-10/](../tasks/phase-10/) and the per-string work list in [inventory.md](../tasks/phase-10/inventory.md).

## Decision
- **Two catalogs, no resx.** `UiTextCatalogFr` and `UiTextCatalogEn` in `Squill.Presentation/Localization` hold the same keys; `UiText` builds a `FrozenDictionary` per language. Keys are English identifiers. No resource files, no reflection, so the design is AOT-safe. Heads reference keys through the `{loc:Text}` markup extension (Avalonia) and `Loc` (WinUI).
- **Placeholders and plurals.** `{0}`, `{1}`... are filled with the language's culture. A countable message is a pair `<key>.one` / `<key>.other` chosen by `FormatPlural`: French "one" is 0 or 1, English "one" is 1.
- **Language setting.** `AppLanguage` (System default / Francais / English) is stored in the settings file. `System` resolves to French when the OS UI language is French, otherwise English (`LanguageResolver`). The environment variable `SQUILL_LANGUAGE=fr|en|system` overrides the setting for one run (tests and captures).
- **Restart required.** The language is read once at start-up (`LanguageSetup`) and strings are resolved when views load. Live switching would need change notification on every bound string in both heads; the Settings page shows a restart note while the choice differs from the active language.
- **Formatting culture follows the language** (`fr-FR` or `en-US`) for numbers, dates and sizes shown in the UI. Exports, generated SQL and files stay invariant.
- **Not translated:** database and driver messages, SQL, identifiers, user data, and developer-only text (argument guards and the like). Messages raised in `Squill.Core` and `Squill.Sql` travel as stable codes (`CoreMessageCodes`) and are translated in Presentation through `issue.*` keys (`IssueText`).
- **Enforcement by tests** (P10-02, P10-03, P10-17, in `tests/Squill.Presentation.Tests`): the two catalogs have identical keys; every key used in `src/` exists in both; no catalog key is unused; a scanner (`HardcodedStringScanner`) finds no hard-coded user text in C# or XAML; and `tests/Squill.Presentation.Tests/i18n/ignored-literals.txt` lists the only allowed exceptions, one `path|exact text|reason` per line. Entries need a reason, an existing file and text still present (stale entries fail), and wildcards are not allowed.
- **Adding a string:** add the key to both catalogs, reference it from code or XAML, run the Presentation tests (see the README).

## Known gaps and what was not verified
- English captures were not made for several screens because the agent sessions could not drive the windows: connection editor and list (P10-10), menus, connected explorer and context menus, Avalonia main window (P10-11), results grid, edit banner and export menu (P10-13), table designer and ER diagram (P10-14), Avalonia settings window (P10-05). To check by hand with `SQUILL_LANGUAGE=en` and the tools in [tools/visual](../../tools/visual/README.md).
- Some literals remain in `ignored-literals.txt` (font families, accelerators, style names, developer-only guards), each with its reason.
- .NET framework messages (for example exception texts) follow the OS language pack, not the app language.
- WinUI strings cannot be fully exercised headless; the WinUI tests check bindings and catalogs, not rendering.
- Long English or French strings were not checked for truncation on every screen.

## Follow-ups (proposals only, nothing decided)
- Live language switching without restart.
- A third language.
- A "Restart now" button on the language row.
- Translated engine (database and driver) messages.
- Right-to-left layout.
