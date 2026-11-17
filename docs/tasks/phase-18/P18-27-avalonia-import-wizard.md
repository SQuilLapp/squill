# P18-27 - Avalonia head: the import wizard window

Status: todo
Depends on: P18-26

## Goal
The same wizard in the Avalonia head. Recipe 6.

## Design decisions (final)
- `Views/ImportWizardWindow.axaml(.cs)` opened through the Avalonia dialog service; step rail, mapping rows with `ComboBox`es, progress page, same numbers as WinUI.

## Tasks
- [ ] Window, pages, wiring.
- [ ] Headless tests in `tests/Squill.App.Avalonia.Tests` (step navigation, mapping row count, both themes render different pixels).

## Acceptance criteria
- Common acceptance (recipe 7) with the Avalonia AOT publish; compare as far as a capture allows and list the rest.

## Suggested commit message
Add the Avalonia import wizard
