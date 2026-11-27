# P20-23 - Table designer: an untouched SQLite table already shows a rebuild script and an enabled `Appliquer`

Status: done
Depends on: P20-19

## Goal
Found by P20-19 on the published Avalonia head (SQLite `-Scenario Full`, dark, French): the designer opened on `clients` or `commandes` with **no edit** already shows the full SQLite rebuild script in the DDL preview (`CREATE TABLE "clients__squill_new"`, `INSERT ... SELECT`, `DROP TABLE`, `RENAME`), an enabled accent `Appliquer`, a dot on the tab, and the warning `Les NULL existants empechent 'id' de devenir non nullable` for the primary key. The draft chip is correctly absent (it compares with the baseline), so `HasChanges` (`TableDesignerViewModel.cs`, `Original` vs the design) is true although nothing changed. Probable cause: the column read from `INTEGER PRIMARY KEY AUTOINCREMENT` is nullable / not auto-increment in `Original` but the design view forces it not null (or the reverse), so `TableDiff.Compute` finds an `id` change. Also seen after `Annuler les modifications`: `Appliquer` stays enabled. Probably the same head (WinUI) and the same Presentation code.

## Design decisions (final)
- Reproduce with a test first in `tests/Squill.Presentation.Tests` (designer view model over a SQLite table with an integer autoincrement key: `HasChanges` is false, `Script` empty, `ApplyCommand.CanExecute` false right after load and after `Revert`), see it fail, then fix the loader or the comparison (not the UI). The existing designer tests must stay green.
- Re-drive with `Drive-Window.ps1` on Avalonia (and WinUI if the card is done on Windows): fresh designer of `clients`: no tab dot, `Appliquer` disabled, empty DDL preview, no NULL warning.

## Out of scope
- The not built designer features; other engines unless the same test fails there.

## Tasks
- [x] Failing test, fix, capture.

## Acceptance criteria
- Recipe 2 common acceptance (`Squill.Windows.slnx` too); captures read.

## Suggested commit message
Do not report changes for an untouched SQLite table in the designer
