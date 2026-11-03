# P10-07 - Number, date and size formatting follows the language

Status: todo
Depends on: P10-02

## Goal
Every number, date, duration and size shown to the user is formatted with the chosen language's culture; machine formats stay invariant.

## Design decisions (final)
- Rule (final): user-visible formatting uses `UiText.Culture` (`fr-FR` for French, `en-US` for English). Nothing relies on `CultureInfo.CurrentCulture` implicitly any more, so the display does not depend on thread culture.
- Stays `CultureInfo.InvariantCulture` (do not touch, add a test that pins them under the English language): CSV and JSON export, SQL and DDL/DML generation, the settings, profile, history and layout files, SVG export coordinates, parsing of edited cell text in `CellValueParser` when the user types into a cell (accepts the invariant form and the active-language form: `1.5` and `1,5` in French, `1.5` in English; decide per the current code and keep behavior unless it breaks the rule, say which).
- Applies to: `CellFormatter` (numbers, decimals, dates and times shown in the grid; `NULL` marker unchanged), footers and counters (`results.footer`, row counts, elapsed time), `HistoryFormat` (Avalonia) and the WinUI history formatting, status bar timings and sizes, explorer counts, any `ToString("N0")`, `ToString("F2")`, `string.Format`, `DateTime.ToString(...)`, `TimeSpan` rendering or interpolated number in `src/Squill.Presentation` and `src/Squill.App.*` that ends up on screen. Find them with grep (`ToString(`, `string.Format`, `$"{`, `CultureInfo`) and list each in the report as changed or kept (with reason).
- Date and time pattern: use the culture's `d`/`g`/`T` standard patterns; no hand-written `dd/MM/yyyy` for display. Duration: keep the existing compact form (`120 ms`, `1.2 s`) but with the culture's decimal separator.
- No new keys: units such as `ms`, `s`, `rows` that are already catalog entries stay there; a unit still hard-coded in a formatter is left to the area card of its screen (it is in the inventory) and only its number formatting is fixed here.

## Out of scope
- Moving literal text into the catalog (P10-10 ... P10-16).

## Tasks
- [ ] Replace implicit culture by `UiText.Culture` at every display site; keep invariant sites untouched.
- [ ] Tests in `tests/Squill.Presentation.Tests` and the head test projects (formatting helpers are headless): the same value formatted under `Override(French)` and `Override(English)` (`1234567.5` -> `1 234 567,5` with the culture's group separator vs `1,234,567.5`; a date; a duration), CSV/JSON export unchanged under both languages.

## Acceptance criteria
- `rtk bypass dotnet build Squill.Windows.slnx -c Release -warnaserror` exits 0 with 0 warnings; `rtk bypass dotnet test --solution Squill.Windows.slnx -c Release` is green.
- No process left running.

## Suggested commit message
Format numbers and dates with the culture of the chosen language
