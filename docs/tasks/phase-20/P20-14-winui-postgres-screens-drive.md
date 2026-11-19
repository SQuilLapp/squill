# P20-14 - WinUI: see the PostgreSQL fixes and drive the PostgreSQL-only screens

Status: todo
Depends on: P20-01, P20-03

## Goal
P19-18 fixed the sessions table widths, the 7 privilege columns, the legend, the singular `1 membre` and the dialog theme, but its Done notes say the widths were "computed, not seen" (no container). Plan rows, the `Failed` variant and the 400-row cut notice (P19-04) were never driven. Use the container of P20-03. Procedure and rules: recipe 3 of [verification-recipe.md](verification-recipe.md).

## Design decisions (final)
- Start `tools/visual/New-PostgresScenario.ps1 -Action Start -Dir <dir>` (P20-03), profile `pg-visual`; WinUI AOT head, 1440 x 900.
- M11 privileges (`new-screens.md` row 5, `P19-18`): all 7 columns visible at 1440 px without horizontal scrolling, legend not cut, hint line `security.hint` present, role with one member reads `1 membre` / `1 member`, the role named `user` produces quoted `GRANT`s (read the generated statements pane), a toggle shows the pending change and the statements, `Appliquer` on a Dev profile and on Production (confirmation, typed name); the schema `archive` (450 tables) shows the cut notice with the hidden count (P19-04). Light, dark, French; English once.
- M12 sessions (row 6): `New-PostgresSessions.ps1 -Count 450 -Block`: columns `Attente` and `Requete` reachable without horizontal scrolling, state dots, the blocking chain, sort and filter, the cut notice for sessions beyond 400, `Annuler la requête` and `Terminer la session` with the themed confirmation (Dev: the app theme, not the system dark dialog; Production).
- Plan (`plan.md` PostgreSQL section rows 1-12 and the `Failed` variant, `transactions.md` rows that were `not driven (no server)`): `Expliquer` / `Analyser` on the join of `plan.md`, misestimate pill, `Analyser` disabled on `update`, cancel of `select pg_sleep(60)`; the `Failed` transaction variant (an error inside `Manuel` mode, then the toolbar status and the `Valider` / `Annuler` state) compared with `M04-*` where a Failed mockup exists, else described. Rows that cannot be reached are `not driven (<why>)`.
- Compare with `M11-*`, `M12-*`, `M06-*`; small fixes with tests (`SecurityViewModelTests`, `SessionsMonitorViewModelTests`, `tests/Squill.App.WinUI.Tests/SecurityViewTests.cs`, `SessionsMonitorViewTests.cs`); bigger ones proposed. Write the WinUI Results in `new-screens.md`, `plan.md`, `transactions.md` (never ticks) and `visual-spec.md` section 17 and 18 cells if they exist (P19-18 left them undone).

## Out of scope
- Avalonia (P20-17), MySQL / SQL Server screens.

## Tasks
- [ ] Start the container and sessions; drive, capture, read PNGs; fixes; update checklists and spec; stop sessions and container (`podman ps -a` empty).

## Acceptance criteria
- Recipe 2 common acceptance if code changed; Done notes say which PostgreSQL rows were seen and which stay open.

## Risks
- Podman machine start time and `fs.aio-max-nr` (see `new-screens.md` MySQL note) concern MySQL only; PostgreSQL should start in seconds.
- 450 sessions slow the monitor: if the poll takes longer than its interval the view must not pile up (P18-13 rule): note anything odd.

## Suggested commit message
Drive the WinUI PostgreSQL screens, privileges and sessions, against their mockups
