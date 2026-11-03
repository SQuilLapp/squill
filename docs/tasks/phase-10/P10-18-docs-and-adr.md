# P10-18 - Documentation: localization ADR 0007, README and architecture note

Status: todo
Depends on: P10-17

## Goal
Record the localization decisions and update the documents that still say "French only".

## Design decisions (final)
- Write `docs/adr/0007-localization.md` (same layout as ADR 0006: Status accepted, Date, Context, Decision, Known gaps, Follow-ups) stating exactly what was built:
  - Two catalogs (`UiTextCatalogFr`, `UiTextCatalogEn`) with identical keys, no resx, no reflection, AOT-safe; English identifier keys; `{0}` placeholders with the language's culture; `.one`/`.other` plurals through `FormatPlural` (French "one" = 0 or 1, English "one" = 1).
  - Language setting `AppLanguage` (System default / Français / English) in the settings file; `System` resolves to French when the OS UI language is French, otherwise English; `SQUILL_LANGUAGE` override for tests and captures.
  - Restart-required switching and why (strings resolved once at load; live switching would need change notification everywhere).
  - Formatting culture follows the language (`fr-FR` / `en-US`); exports, SQL generation and files stay invariant.
  - What is not translated: database and driver messages, SQL, identifiers, user data, developer-only guards; Core/Sql messages travel as stable codes with `issue.*` keys.
  - Enforcement: parity, used-keys, scanner and XAML tests (P10-02, P10-03, P10-17), the ignore list and its rules.
  - Known gaps from the cards' reports (screens that could not be driven, strings left in the ignore list, .NET framework messages that follow the OS language pack not the app language, WinUI headless limits).
  - Follow-ups (proposals only): live switching, a third language, a restart button, translated engine messages, right-to-left.
- Update ADR 0006: change the "French only" bullet to say that phase 9 shipped French only and that ADR 0007 superseded it (add "Superseded in part by ADR 0007" under Status; keep the rest of the text).
- Update `docs/design/visual-spec.md` section 2 if P10-16 did not, `README.md` (languages, the `SQUILL_LANGUAGE` variable, how to add a string), `docs/architecture.md` (Presentation localization paragraph) and `docs/tasks/README.md` (phase 10 status, final decisions).

## Out of scope
- Code changes.

## Tasks
- [ ] ADR 0007, edits of ADR 0006, README, architecture, visual-spec.
- [ ] Set every phase 10 card and index line to `done`.

## Acceptance criteria
- `rtk bypass dotnet build Squill.slnx -c Release -warnaserror` still exits 0 (docs only; run once).
- Links in the new ADR resolve (check each relative path exists).
- No process left running.

## Suggested commit message
Document the localization decisions in ADR 0007
