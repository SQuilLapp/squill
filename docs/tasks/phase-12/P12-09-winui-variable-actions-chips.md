# P12-09 - WinUI head: quick actions, chip row and the variable dialog

Status: todo
Depends on: P12-08

## Goal
The user can act on a variable from the editor: click it (or `Ctrl+.`), use the chip row, create or edit it in a chosen scope. Limits: [variables-spec.md](variables-spec.md) section 7.

## Design decisions (final)
- Click detection (no pointer event in the package): `LoomSqlEditor` raises `VariableActivated` when `CaretChanged` puts an empty selection **inside** a variable range (not at its edges) after a 250 ms delay, and immediately on `Ctrl+.` (`KeyDown` handler on the host grid with `handledEventsToo`, because LoomEditor handles keys). A caret moved by arrow keys through a variable must not open the flyout: only open when the last input was a pointer press (track `PointerPressed` on the host grid with `AddHandler(..., handledEventsToo: true)`), or `Ctrl+.`.
- Flyout: a XAML `MenuFlyout` built from `VariableQuickActions` (P12-07): header line with `{{name}}` and the state, then the actions (Edit value, Create in Global / Environment / Connection, Set for this tab only, Make secret, Open variables manager). Anchored on the host `Grid` at the pixel position of the caret if the package exposes one (look for a position-to-point method on the `LoomEditor` control; if none, anchor at the pointer position recorded by `PointerPressed`, else under the editor top-left) and say in the report which one was used.
- Chip row: a horizontal `ItemsControl` under the editor in the query tab view bound to `VariableChipsViewModel`; each chip is a pill with the state colour tokens of P12-08; click opens the same flyout anchored to the chip; the row is collapsed when there are no variables. It is also the fallback when click detection fails.
- `PromptVariableAsync` implemented as a `ContentDialog` (name read-only for edit, scope `ComboBox` with disabled entries and reasons, value `TextBox` / `PasswordBox` when secret, Secret and Allow raw checkboxes with a warning text for raw), styled with the shared control styles of P9-06. Validation through the service result (`variables.error.*`).
- Query tab: when a run needs unresolved variables the parameter prompt of P12-05 labels them as variables (add the label in the existing WinUI parameter dialog).
- All strings `UiText`/`Loc` (phase 10 rules, `docs/tasks/phase-10/migration-recipe.md`).

## Out of scope
- Variables manager window (P12-10), Avalonia.

## Tasks
- [ ] Activation logic, flyout, chip row XAML, dialog, parameter dialog label, catalog keys fr/en.
- [ ] Tests for the activation decision (pure function: last input kind, selection, range) and for the chip view-model hookup; capture screenshots (en/fr, light/dark) of the chip row and the dialog; "to check by hand": click a resolved, an unresolved and a secret variable and run a query, with the exact clicks.

## Acceptance criteria
- `rtk dotnet build Squill.Windows.slnx -c Release -warnaserror` 0 warnings; WinUI AOT publish clean; tests green; no process left running.

## Suggested commit message
Add variable quick actions, chip row and edit dialog to the WinUI head
