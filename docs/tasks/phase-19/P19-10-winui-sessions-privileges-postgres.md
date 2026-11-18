# P19-10 - WinUI: look at M11 (privileges) and M12 (sessions) on a PostgreSQL container

Status: done
Depends on: P19-07, P19-08

## Goal
M11 and M12 need a live PostgreSQL server; no capture of either was ever compared with `M11-Droits-*` / `M12-Sessions-*` (`docs/verification/new-screens.md`, rows 5 and 6, "No PostgreSQL container was kept with `-Keep` for a window pass"). Recipe 3 of [debug-polish-recipe.md](debug-polish-recipe.md) applies.

## Design decisions (final)
- Container: `rtk pwsh eng/run-docker-tests.ps1 -Engine postgres -Keep` (Podman, `docs/testing.md`; run it in the background, it waits for Enter). It prints host, port, user, password. Create a SQuilL profile for it **through the app's connection editor or `profiles.json` in the scratch data dir** with the printed values; never write those values into any repository file, card or commit. If Podman is unavailable or the VM reports `io_setup() failed with EAGAIN`, apply the `fs.aio-max-nr` fix of `new-screens.md`; if still unavailable, stop and report (nothing is marked verified).
- Data: M12 needs an open transaction with an `UPDATE` in one connection and a second `UPDATE` of the same row blocked in another (two psql / `podman exec` sessions, or a second SQuilL tab) plus an idle-in-transaction session; M11 uses the roles of the P18-17 scenario (`tests/Squill.Data.Postgres.Tests`, find the role names in it). For P19-04's notice, also create 450 `pg_sleep` sessions only if cheap (`DO` block with `dblink` is not available: use a loop of background `psql -c "select pg_sleep(60)"` processes), otherwise leave it to the owner and say so.
- Matrix: M11 and M12 in WinUI, light and dark, French and English. Rows 5, 6 and 7 of `new-screens.md`; for M12 also the confirmations of `Annuler la requête` and `Terminer la session` (drive up to the confirmation dialog; ending the blocking session is allowed on the throwaway container) and the Production variant (red top line: a second profile marked `Production` pointing at the same container). For M11: toggle a cell, read the pending outline and the generated SQL panel, `Appliquer` on the container, then check with `\dp` (or `has_table_privilege`) that the grant took effect.
- Small differences fixed here, bigger ones proposed (`P19-17` onwards). Record results per recipe 3 and update the M11 / M12 rows of `visual-spec.md` (sections 17, 18).

## Out of scope
- MySQL / SQL Server monitor, Avalonia (P19-11), the 400-row limit (P19-04/05).

## Tasks
- [x] Start the container, prepare roles and blocked sessions, drive and capture; read every PNG.
- [x] Fixes, proposed cards, `new-screens.md` and `visual-spec.md` updates.
- [x] Stop the container (Enter in its terminal), `podman ps -a` shows nothing from this run, background `psql` processes ended, app closed.

## Acceptance criteria
- Common acceptance (recipe 4) if code changed; report lists compared / not driven; no credential printed or committed.

## Risks
- Killing the wrong session: only terminate sessions you created on the throwaway container.
- The mockup shows more rows than a small seed: compare structure and states, not row counts.

## Suggested commit message
Compare the WinUI sessions and privileges screens with their mockups on PostgreSQL

## Done notes
Compared and driven on a PostgreSQL 17 container; results and the differences are in the Note of `docs/verification/new-screens.md` (rows 5 to 7); gaps proposed as P19-18. Podman: the machine had to be started; the container was run by hand (`eng/run-docker-tests.ps1 -Keep` waits on stdin, which a background shell does not provide), removed at the end. No code changed.
