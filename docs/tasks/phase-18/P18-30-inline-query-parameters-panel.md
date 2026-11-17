# P18-30 - Query parameters panel under the editor (both heads)

Status: todo
Depends on: none

## Goal
M04 draws the query parameters prompt as a panel under the editor (`Paramètres de la requête`, rows with `:name`, type dropdown, value field, `Transmettre NULL`, `Ignorer`, `Exécuter avec ces valeurs`). Both heads still show a modal dialog (`visual-spec.md` section 12, M04). Move it into the tab.

## Design decisions (final)
- Presentation (P4-08 `QueryParameter` prompt view-model): add `ParametersPanel` state to `QueryTabViewModel` (`IsParametersPanelOpen`, the existing parameter rows, `Run` / `Ignore` commands, the validation `Valeur requise`); running a statement with unresolved parameters opens the panel instead of the dialog service call; `Ignorer` closes it and runs nothing; `Exécuter avec ces valeurs` closes it and runs. Remembered values per file (`valeurs mémorisées pour ce fichier`) keep their current behavior. The dialog contract method stays for any other caller but the query tab no longer uses it; remove it only if nothing else calls it.
- Heads: a panel under the editor, above the results (M04 numbers: title, `{0} détectés . valeurs mémorisées pour ce fichier` muted count, rows 165-wide type dropdown, value field, checkbox, danger validation text, ghost and primary buttons). WinUI: plain-string `ComboBox` items (recipe 5). Focus goes to the first empty value; `Échap` = `Ignorer`; `Ctrl+Entrée` in a value field = `Exécuter avec ces valeurs`.
- The production confirmation and the transaction flow are unchanged: they run after the panel's `Exécuter`.

## Out of scope
- New parameter features, the other M04 gaps (wavy underline, inline pill, popup styling), which depend on the editor controls and stay open.

## Tasks
- [ ] View-model change and tests (`QueryTabViewModel` parameter flow: panel opens, ignore runs nothing, run executes, validation, remembered values, a second run with the same values does not reopen it only if the existing rule says so).
- [ ] WinUI and Avalonia panel views, tests in both head test projects.

## Acceptance criteria
- Common acceptance (recipe 7) with both AOT publishes; capture the panel in both heads next to `M04-Editeur-*`.

## Suggested commit message
Show the query parameters in a panel under the editor
