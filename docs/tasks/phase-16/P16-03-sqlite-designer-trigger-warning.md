# P16-03 - Table designer: warn when a SQLite trigger names a renamed or dropped column

Status: todo
Depends on: none

## Goal
Altering a SQLite table rebuilds it and replays the triggers with their old text (`SqliteDdlGenerator`, `shortcut:` "trigger bodies keep their old text, so one that names a renamed or dropped column fails when it fires"). The failure shows up later, on an unrelated `INSERT`. The designer must say so before the user applies.

## Design decisions (final)
- Warn, do not rewrite (rewriting trigger bodies needs a real parser of trigger bodies; rejected). The designer view-model (`src/Squill.Presentation/Designer/TableDesignerViewModel.cs`, where `Issues` and the `DesignViewsNotRecreated` warning are built, around the `issues.Add` calls) adds one `DesignIssueSeverity.Warning` per (trigger, column) pair: the table's `TableDesign.Triggers` texts are tokenized with the SQLite tokenizer (`Squill.Sql`, the Presentation project already references it); a pair exists when an identifier token (quotes removed, case-insensitive) equals the original name of a column that the diff drops or renames. SQLite only (other dialects do not rebuild and keep trigger text valid or error at apply time); no warning when nothing was dropped or renamed.
- New message code `CoreMessageCodes.DesignTriggerMentionsColumn` with arguments `[triggerName, columnName]` (trigger name parsed from `CREATE TRIGGER <name>` tokens, falls back to `"?"`), texts in both catalogs (`UiTextCatalogFr` / `En`, parity tests must stay green), shown like the existing warning, non-blocking (`HasErrors` unchanged), and listed in the confirmation of `Apply` the way other warnings already are (line 229 of the view-model).
- A column referenced only as part of a longer identifier never matches (token equality, not substring). False positives (a column name that is also a keyword or a table alias in the trigger) are acceptable for a warning and stated in the issue text ("may use").
- `SqliteDdlGenerator` is not changed except removing nothing: keep the `shortcut:` comment and extend it with "the designer warns (P16-03)".

## Out of scope
- Rewriting trigger bodies, views depending on the table (already warned), other engines.

## Tasks
- [ ] Message code, both catalogs, warning generation in the view-model.
- [ ] Tests in `tests/Squill.Presentation.Tests/Designer/`: rename warns, drop warns, add-column does not, a trigger on another table does not, `col_x` does not match `col`, PostgreSQL design with the same data produces no warning, `HasErrors` stays false, apply goes through after the warning is shown.

## Acceptance criteria
- Common acceptance; localization completeness guards green (P10-17).

## Suggested commit message
Warn in the table designer when a SQLite trigger uses a column that is renamed or dropped
