# P21-29 - Query parameters: `${name}` text variables and values kept across tabs

Status: todo
Depends on: none

## Goal
Close the gap with DataGrip's user parameters: besides bound parameters, `${name}` variables replaced as **text** before the run (for a table name, a schema, a list), and the last values remembered for the connection, not only the tab.

Exists today (most of the feature): `ParameterScanner` finds `:name`, `@name`, `$name` (tokenizer-based, strings and comments ignored) and rewrites them to `@name` bound parameters; the parameters panel under the editor (P18-30, `ParameterRowViewModel`, `IsParametersPanelOpen`) asks values before the run, with `NULL`; last values are kept per tab (`_lastValues`); positional `?` / `$1` are refused. Gap: text variables, cross-tab memory, a type hint.

## Design decisions (final)
- `${name}` (also inside strings, like DataGrip, because it is plain text substitution): scanned with a regex outside comments; the panel row shows a `text` badge and the value is inserted verbatim (no quoting) before parameter scanning. A value with `;` or a newline is refused with a row error (no statement smuggling); the substituted SQL is what history records.
- Values remembered per connection in memory for the app session (`ConnectionSession.ParameterValues`), pre-filled in any tab of that connection; not saved to disk.
- Row type hint (`text`, `number`, `date`, `bool`) chosen in the row, used only to validate the input (the provider still binds a string as today).
- Both heads: the badge and the type combo in the existing panel rows (small; one card covers Presentation and both heads).

## Out of scope
- Saving values to disk, parameter sets.

## Tasks
- [ ] Tests: substitution, `;` refused, mixed `${a}` and `:b`, cross-tab pre-fill, type validation; head tests (text check WinUI, headless Avalonia); drive both exes (recipe 3).
- [ ] README line, `CHANGELOG.md` line.

## Acceptance criteria
- Recipe 2 and 3 acceptance for both heads.

## Risks
- Substitution changes offsets: run-under-caret is resolved before substitution.

## Open points (defaults)
- Owner: refuse `;` in values (default yes).

## Suggested commit message
Add ${name} text variables and keep parameter values per connection
