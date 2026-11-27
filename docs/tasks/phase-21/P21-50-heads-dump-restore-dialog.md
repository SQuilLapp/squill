# P21-50 - WinUI and Avalonia: export DDL, dump and restore dialogs

Status: todo
Depends on: P21-48, P21-49

## Goal
Reach the dump and restore features from the explorer in both heads.

Exists today: explorer menus on connection, database and schema rows; the import wizard windows (P18-26, P18-27) as the multi-step dialog pattern.

## Design decisions (final)
- Explorer menu on a database/schema: `Export DDL...`, `Dump...`, `Restore...`; on a table: `Export DDL...`.
- Dump dialog: method (`Generated SQL` / engine tool with its detected version or `not found`), tables check list, options, file path, `Start`; a progress view with the log; `Cancel`.
- Restore dialog: file, target, method; starts the script runner or the tool; production confirmation as usual.
- `DumpDialogViewModel` and `RestoreDialogViewModel` are written in this card (Presentation, tests first) over the services of P21-48 and P21-49; then both heads draw them. If the card runs past half a session after the view-models, stop and split the heads into a `P21-97+` card (status `partial`).

## Out of scope
- None.

## Tasks
- [ ] Tests; drive both exes (recipe 3) on the SQLite scenario (generated dump and `VACUUM INTO`).
- [ ] README section "Backup and restore" (short), `CHANGELOG.md` line.

## Acceptance criteria
- Recipe 2 and 3 acceptance for both heads.

## Risks
- File pickers not drivable: type the path.

## Open points (defaults)
- None.

## Suggested commit message
Add the export DDL, dump and restore dialogs to both heads
