# P18-27 - Avalonia head: the import wizard window

Status: done
Depends on: P18-26

## Goal
The same wizard in the Avalonia head. Recipe 6.

## Design decisions (final)
- `Views/ImportWizardWindow.axaml(.cs)` opened through the Avalonia dialog service; step rail, mapping rows with `ComboBox`es, progress page, same numbers as WinUI.

## Tasks
- [x] Window, pages, wiring.
- [x] Headless tests in `tests/Squill.App.Avalonia.Tests` (step navigation, mapping row count, both themes render different pixels).

## Acceptance criteria
- Common acceptance (recipe 7) with the Avalonia AOT publish; compare as far as a capture allows and list the rest.

## Suggested commit message
Add the Avalonia import wizard

## Result
- Avalonia AOT publish of `src/Squill.App.Avalonia`: no IL2xxx/IL3xxx warning from SQuilL code (only the existing third-party `Microsoft.Data.SqlClient` / `System.Configuration.ConfigurationManager` package warnings), the exe starts and responds.
- Window built in code like the WinUI one (`Views/ImportWizardWindow.cs`, no axaml: the pages are rebuilt per step); brushes are bound to theme resources so light and dark follow the variant. `AvaloniaDialogService` opens it and picks CSV files.
- Capture of the mapping step against M10: **to check by hand** (needs the native file picker and a live connection). Same known differences as P18-26: `Lignes en erreur` on the Options step, no value mapping, conflict, pause or transfer card, the rail subtitle shows the engine enum name.
- Tests: `ImportWizardUiTests` (4 headless: step navigation, one string combo per mapping row, Enter never starts the run, light and dark render different pixels).
