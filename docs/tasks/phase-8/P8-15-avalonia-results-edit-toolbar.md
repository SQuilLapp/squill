# P8-15 - Avalonia head: edit toolbar, issues and commit

Status: todo
Depends on: P8-14

## Goal
The commands around an editable grid on the Avalonia head: add, delete, revert, preview, commit, discard, and the visible pending-change state.

## Design decisions (final)
- In the results panel (P5-06), for a result with an `Editor`, show an edit bar above the grid: buttons `Add row` (`EditController.AddRow`), `Delete rows`, `Revert rows`, `Preview SQL` (`Editor.PreviewScriptCommand`), `Commit` (`Editor.CommitCommand`), `Discard` (`Editor.DiscardCommand`), then the `StatusText`, and an error-glyph button showing `Issues.Count` that opens a flyout list of the issues (`Row <n+1>, <column>: <message>`; selecting one selects that cell in the grid). Results with `ReadOnlyReason` show a muted `Read-only: <reason>` label in the same place and no buttons. Results without either show nothing. Tokens only, existing button styles.
- `Ctrl+S` while the results panel has focus runs `Editor.CommitCommand`. `Editor.ScriptRequested` opens the script in a new query tab through the same path the designer's `ScriptRequested` uses (P7-22; reuse the workspace method, do not duplicate).
- The grid switches to `DisplayGrid` for results that have an editor (P8-12); nothing else about export changes.
- A short per-row marker is not needed beyond the colours of P8-14.

## Out of scope
- WinUI (P8-16), column resizing, filters.

## Tasks
- [ ] Implement the edit bar, issues flyout, shortcuts and script routing.
- [ ] Headless tests in `tests/Squill.App.Avalonia.Tests`: bar visible only for editable results, read-only label for others, Add row adds a pending row and enables Commit, an issue disables Commit and appears in the flyout, `Ctrl+S` runs commit with a fake connection, `Preview SQL` opens a tab with the generated script.
- [ ] Run the AOT-published head on a temp SQLite file: open a table with a primary key, edit a cell, add a row, delete a row, commit, re-run the query and check the values. State in the card report what was and was not exercised (no UI automation may mean only startup was verified). Close every process you started.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).
- `rtk dotnet publish src/Squill.App.Avalonia -c Release -r win-x64` succeeds with zero IL2xxx/IL3xxx warnings from Squill code.
- No process left running.

## Suggested commit message
Add edit toolbar and commit to the Avalonia results panel
