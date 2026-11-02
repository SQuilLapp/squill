# P9-03 - Presentation: French string catalog

Status: todo
Depends on: none

## Goal
A single AOT-safe place for every user-visible string, filled with the French of the mockups, used by both heads.

## Design decisions (final)
- Decision (final): the UI is French only in phase 9 (`docs/design/visual-spec.md` section 2). No resx, no `ResourceManager`, no reflection.
- In `Squill.Presentation/Localization/`: `UiText` (public static class) with `string Get(string key)` and `string Format(string key, params object?[] args)` (culture `fr-FR` for number and date formatting, regardless of the OS culture), backed by a generated-at-build-free constant table: one `FrozenDictionary<string, string>` built in a static constructor from the entries of `UiTextCatalog.cs` (a plain C# file of `["key"] = "valeur"` entries, not JSON). Unknown key: `Get` returns `!key!` and a test fails if any key used in `src/` is missing (scan source for `UiText.Get("...")` literals in a test).
- Keys are English dotted identifiers (`menu.file`, `explorer.title`, `results.exportRows`), grouped by screen in comments. Placeholders use `{0}`. Plurals are separate keys (`results.pendingChanges.one` / `.other`).
- Fill the catalog with every French string listed in `docs/design/visual-spec.md` sections 4 to 9 that belongs to a feature the app has, with exactly the spelling and punctuation drawn (non-breaking spaces are not used; the apostrophe is the straight `'`).
- Heads bind through `UiText` (code) and a tiny markup helper: Avalonia `{loc:Text explorer.title}` markup extension (compile-time friendly, no reflection) and WinUI an attached property `Loc.Key` or code-behind assignments (choose the one that works with Native AOT and compiled bindings, say which in the report).
- Existing English strings of the screens in scope are replaced by the later cards, not by this one.

## Out of scope
- Replacing existing strings in views, an English catalog, a language setting.

## Tasks
- [ ] Implement `UiText`, the catalog, the two head helpers (one small file each).
- [ ] Tests in `tests/Squill.Presentation.Tests`: Format with `{0}`, missing key, plural keys, catalog keys unique, every literal key used in `src/` exists.

## Acceptance criteria
- `rtk bypass dotnet build Squill.Windows.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.Windows.slnx -c Release` is green.
- Both head publishes succeed with zero IL2xxx/IL3xxx warnings.
- No process left running (app, `dotnet build-server`, AOT executable).

## Suggested commit message
Add the French UI string catalog
