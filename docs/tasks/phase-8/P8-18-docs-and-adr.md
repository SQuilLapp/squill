# P8-18 - Documentation: grid editing ADR, README and architecture note

Status: done
Depends on: P8-15, P8-17, P8-13, P7-27

## Goal
Record the design decisions of phase 8 and document the feature for users.

## Design decisions (final)
- New `docs/adr/0005-grid-editing.md` in the format of the existing ADRs (read `0004-schema-designer.md`): context, decision, not covered, consequences. It records: editing is allowed only for a single-table `SELECT` whose primary key is selected, decided by parsing the SQL (`EditableSourceAnalyzer`), with the exact list of read-only reasons; pending changes live in an overlay (`EditableGridDataSource`) that never mutates the streamed result and are applied only after the load finished; row edits are an abstract `RowEdit` list, translated by one hand-written parameterized generator (`@pN` placeholders for all dialects, values never inlined, no ORM, no reflection); updates and deletes are keyed on the primary key only, so a concurrent change of other columns is overwritten (last write wins) while a vanished row is detected through the affected-row count and rolls the commit back; execution order delete, update, insert in one transaction (non-transactional MySQL engines excepted); generated keys read back per dialect (`RETURNING`, `OUTPUT INSERTED`, `LAST_INSERT_ID()`), only one generated key column; text typed by the user is converted by `CellValueParser` by CLR type, other types are passed as text; the shared `GridEditController` keeps both heads identical; typed parameter limits found by P8-13; what is not covered (joins, views, tables without primary key, non-key generated columns read-back, concurrency tokens, bulk edit, undo stack, export of pending changes); reopening condition.
- `README.md`: a short "Editing data" section (what is editable and why a result can be read-only, keys, commit and discard, preview SQL, the last-write-wins note), matching the existing README tone; do not restructure the file.
- `docs/architecture.md`: update "Order of work" to say phase 8 is done and link the ADR; add nothing else.
- No code changes.

## Out of scope
- New features, screenshots that cannot be produced in the session.

## Tasks
- [x] Write the ADR, update the README and architecture note, check every link.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings (no code touched; confirms the tree is still green).
- All relative links in the changed documents resolve.

## Suggested commit message
Document grid editing
