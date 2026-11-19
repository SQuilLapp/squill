# P20-17 - Avalonia: drive privileges (M11), sessions (M12) and the PostgreSQL plan rows

Status: todo
Depends on: P20-14

## Goal
`new-screens.md` rows 5 and 6 say `agent (Avalonia): not driven`; the Avalonia head never ran against a PostgreSQL server in a pass, and P19-14 left PostgreSQL plan rows and the `Failed` variant open. Repeat P20-14 on Avalonia, with the container of P20-03. Procedure and rules: recipe 3 of [verification-recipe.md](verification-recipe.md).

## Design decisions (final)
- Head: Avalonia AOT exe on Windows, profile `pg-visual`, French light and dark, English once; read the Done notes of P20-14 first and reuse its findings (shared Presentation fixes are already in).
- Same areas as P20-14: M11 (7 columns at 1440 px logical, legend, hint line, `1 membre`, role `user` quoting, toggle and `Appliquer` on Dev and Production, the `archive` schema cut notice), M12 (450 sessions, blocking chain, `Attente` and `Requete` reachable, cut notice, cancel and kill with the themed confirmation), plan rows and the `Failed` variant. `PrivilegeMatrixCanvas` and the sessions table are custom drawn: drive them with coordinates.
- Compare with `M11-*`, `M12-*`, `M06-*`; small fixes with tests (`tests/Squill.App.Avalonia.Tests/SecurityViewUiTests.cs`, `SessionsMonitorUiTests.cs`, `PlanUiTests.cs`); propose bigger ones. Write Avalonia Results in `new-screens.md`, `plan.md`, `transactions.md` (never ticks) and `visual-spec.md` sections 17 and 18.

## Out of scope
- WinUI (P20-14), MySQL / SQL Server screens, Linux / macOS.

## Tasks
- [ ] Start the container and sessions; drive, capture, read PNGs; fixes; update checklists and spec; stop sessions and container (`podman ps -a` empty).

## Acceptance criteria
- Recipe 2 common acceptance if code changed; report lists compared / not driven per area.

## Risks
- Same as P20-14; the Avalonia head's status bar thread guard (P18-34) must not fire a crash dialog under 450 sessions: watch for it.

## Suggested commit message
Drive the Avalonia PostgreSQL screens, privileges and sessions, against their mockups
