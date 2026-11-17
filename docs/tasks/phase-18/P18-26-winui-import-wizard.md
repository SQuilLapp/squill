# P18-26 - WinUI head: the import wizard window

Status: todo
Depends on: P18-25, P9-18

## Goal
Draw M10 in the WinUI head. Recipe 5, 6.

## Design decisions (final)
- `Views/ImportWizardWindow.xaml(.cs)` opened through the dialog service (same pattern as the connection manager and the production dialogs of phase 15 / 16): left step rail (262 wide, title `Importer un fichier`, subtitle `CSV -> table PostgreSQL` from the engine name), content area with a title `Mappage des colonnes` + `Étape 3 sur 5`, footer with the summary text and `Annuler`, `Retour` + chip, primary `Suivant : Options` + chip.
- The mapping table is built in code (`Grid` rows with a `ComboBox` per row whose items are **plain strings** mapped back by index, recipe 5; the P17-19 crash was exactly an object list in a `ComboBox`). Warning rows tinted with the `warning-soft` token (add to the tokens if missing).
- Progress step with a `ProgressBar` and live counts; `Entrée` never starts the run on the last step by accident (the run starts only from the explicit button).

## Tasks
- [ ] Window, step pages, mapping rows, progress page, dialog service wiring.
- [ ] Tests in `tests/Squill.App.WinUI.Tests` where the harness allows (step navigation, row count, string combos).

## Acceptance criteria
- Common acceptance (recipe 7) with the WinUI AOT publish; capture the mapping step with a real CSV against SQLite and compare with M10 light and dark; list differences.

## Suggested commit message
Add the WinUI import wizard
