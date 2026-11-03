# P10-16 - Migrate strings: settings, dialogs, errors and every leftover

Status: todo
Depends on: P10-10 ... P10-15, P10-05, P10-06

## Goal
Close the inventory: the remaining settings-window texts, the dialog services, workspace-level confirmations and errors, application start-up errors, and every line of area `other`.

## Design decisions (final)
Follow `docs/tasks/phase-10/migration-recipe.md`. Areas `settings-dialogs-errors` and `other` of `inventory.md`.
- Files in scope (the inventory is authoritative): `SettingsViewModel` (`SaveError` heading, the raw I/O detail after it stays verbatim), both `SettingsWindow` views (labels, theme and keyword-case option names: catalog keys instead of enum `ToString()`), `AvaloniaDialogService` and the WinUI dialog service (default button captions OK / Cancel / Yes / No, dialog titles), `WorkspaceViewModel` (Connection failed, Close tab, Close designer, Drop table, Drop failed, "Loading the diagram layout failed", terminal unavailable / not started: the `reason` text produced by the terminal launcher is shown after a translated heading), `AppComposition` and `Program` start-up or fatal error text, About text, `ConnectionList` leftovers, any literal the previous cards did not take.
- At the end every inventory line of every area is ticked; a line whose area card forgot it is migrated here and named in the report.
- Developer-only text (`ArgumentException` guards and the like) stays English and is listed in `ignored-literals.txt` with its reason; nothing else may be ignored without a reason.
- Update `docs/design/visual-spec.md` section 2 ("French only") to "French and English" with one sentence pointing to ADR 0007 (written by P10-18).

## Out of scope
- Enforcement tests (P10-17), ADR (P10-18).

## Tasks
- [ ] Migrate the remaining inventory lines and tick them; `grep -c "\[ \]" docs/tasks/phase-10/inventory.md` returns 0.
- [ ] Tests per recipe rule 7 (every dialog helper in both languages, save error heading with raw detail, terminal unavailable).
- [ ] English captures: settings window (both heads), a confirmation dialog, an error dialog, the connection-failed dialog.

## Acceptance criteria
- Common criteria of the recipe; additionally both head publishes succeed with zero IL2xxx/IL3xxx warnings.

## Suggested commit message
Localize settings, dialogs and error messages
