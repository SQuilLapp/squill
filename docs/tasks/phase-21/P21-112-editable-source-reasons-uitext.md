# P21-112 - Editor: localise the read-only reasons of EditableSourceAnalyzer

Status: todo
Depends on: P21-12

## Goal
The reason shown next to "Lecture seule" / "Read-only" is translated, in both catalogs, instead of an English literal.

Exists today: `EditableSourceAnalyzer.Analyze` (Squill.Sql) returns `EditableSourceResult(Target, Reason)` where `Reason` is an English literal ("not a single statement", "not a plain SELECT", "query is aggregated or uses WITH/DISTINCT", "query does not read a single table", "source is not a table", "table has no primary key", "duplicate column names", "primary key column X is not selected"); `QueryTabViewModel` (line ~603) hands it to `ResultViewModel.SetReadOnly`, so a French session shows "Lecture seule - not a single statement". Found while driving P21-106; P21-12 fixed the cause for `select first N` but not the text.

## Design decisions (final)
- `Squill.Sql` has no `UiText`: `EditableSourceResult` carries a stable reason code (and the key column name as an argument) instead of text; `QueryTabViewModel` maps the code to a `UiText` key (`query.readOnly.<code>`), FR and EN.
- Keep `query.notEditable` as the fallback.

## Out of scope
- New editability rules.

## Tasks
- [ ] Tests first: each reason code, catalog parity and used-keys tests.
- [ ] Both catalogs, string scanner green.

## Acceptance criteria
- Recipe 2 acceptance; the existing editable-source tests keep their meaning (assert codes instead of text).

## Suggested commit message
Translate the read-only reasons of the editable source analyzer
