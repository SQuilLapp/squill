# P11-36 - End-to-end verification: SQLite (WinUI first, Avalonia smoke)

Status: automated part done, manual part pending the owner (checklist `docs/verification/sqlite.md`)
Depends on: P11-10, P11-13, P11-21, P11-25, P11-29, P11-33

## Goal
Drive every feature of the app by hand-equivalent automation against a SQLite file and write the verification report; this is where the edit flow and popups never driven before get driven. No container needed.

## Design decisions (final)
- Method: `verification-recipe.md` section 2. Database: a file created from `tests/Shared/SampleSchema/sqlite.sql` in the scratch folder.
- Feature list (each row of `docs/verification/sqlite.md`): create, edit and delete a connection (file picker button, validation messages fr and en); connect and reconnect; explorer (tables, views, indexes, FKs, triggers, counts, refresh); open table; run a statement, several statements, Run All, run under cursor, parameters prompt (`:name` and `?`), cancel a long statement, a failing statement (error text, caret); result grid (scroll 100k rows, sort if present, select, copy, export CSV and JSON through the file dialog); grid editing (edit a cell, add, delete, discard, commit, key column edit, read-only table, issues popup, commit failure); table designer (new table, alter, preview SQL, apply, failure); ER diagram (open, drag, save layout, export SVG); completion, hover, diagnostics, format; history (re-run, search, delete), snippets (save, insert); settings (theme, language, formatter case, restart text); terminal tab (message when `sqlite3` absent); window chrome (menu, rail, status bar, closing tabs with unsaved edits asks, dialogs).
- WinUI is driven for every row; Avalonia gets a smoke pass (connect, run, grid, edit, designer, ER) and the rows it fails are marked.
- Output: `docs/verification/sqlite.md` and the list of fixes made (each with its regression test) or new cards (`P11-41` onwards).

## Out of scope
- Features absent from the app (record them as findings only).

## Tasks
- [x] Automated part: `QueryFlowScenario` through the real view models on a live file; two bugs fixed (rows-affected text, SQLite UI-thread block on a slow statement).
- [ ] Manual part: the owner ticks `docs/verification/sqlite.md` section 3 in the WinUI window (no session can drive a Windows UI); Avalonia rows are smoke.

## Acceptance criteria
- Report complete: every row has a result; no row is empty. `rtk dotnet build Squill.Windows.slnx -c Release -warnaserror` 0 warnings; `rtk bypass dotnet test --solution Squill.Windows.slnx -c Release` green; WinUI and Avalonia AOT publishes with zero IL2xxx/IL3xxx warnings; no process left running.
- To check by hand (owner): the report's "to check by hand" list, max 10 steps.

## Suggested commit message
Verify SQLite end to end and record the report
