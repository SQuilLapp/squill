# P8-17 - WinUI head: edit toolbar, issues and commit

Status: todo
Depends on: P8-16, P8-15

## Goal
The WinUI counterpart of P8-15.

## Design decisions (final)
- Same edit bar, labels, behaviour and shortcuts as P8-15 in the WinUI results panel (P5-12), built with `x:Bind` and existing button styles and tokens: `Add row`, `Delete rows`, `Revert rows`, `Preview SQL`, `Commit`, `Discard`, `StatusText`, an issues button with a `Flyout` list (selecting an issue selects the cell), a muted `Read-only: <reason>` label for read-only results.
- `Ctrl+S` (a `KeyboardAccelerator` on the panel) runs `Editor.CommitCommand`; `Editor.ScriptRequested` opens a new query tab through the same workspace method as P7-26 uses for the designer.
- Build and test with `Squill.Windows.slnx`.

## Out of scope
- Anything in the Avalonia head.

## Tasks
- [ ] Implement the edit bar, flyout, shortcuts and script routing.
- [ ] Tests in `tests/Squill.App.WinUI.Tests` for the testable parts; run the AOT-published head on a temp SQLite file and edit, add, delete and commit a row if the session allows UI driving, otherwise verify startup only. State in the card report what was and was not exercised. Close every process you started.

## Acceptance criteria
- `rtk dotnet build Squill.Windows.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.Windows.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).
- `rtk dotnet publish src/Squill.App.WinUI -c Release -r win-x64` succeeds with zero IL2xxx/IL3xxx warnings.
- No process left running.

## Suggested commit message
Add edit toolbar and commit to the WinUI results panel
