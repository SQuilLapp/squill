# P20-03 - One script that starts PostgreSQL with the data of the PostgreSQL verification passes

Status: todo
Depends on: none

## Goal
P19-10 had to start Podman and run a container by hand (`eng/run-docker-tests.ps1 -Keep` waits on stdin, which a background shell does not provide); P19-18 and ADR 0016 list the PostgreSQL scenarios nobody saw: the privilege matrix with all 7 columns, the sessions table widths, blocking chains, the plan `Failed` variant and plan rows, and the 400-row cut notice (more than 400 objects or sessions). Prepare the engine and its data once so the PostgreSQL drives (P20-14, P20-17) start from the same state.

## Design decisions (final)
- `tools/visual/New-PostgresScenario.ps1 -Action Start|Stop|Status [-Port 55432] [-Name squill-visual-pg]` (PowerShell 7). `Start`: checks `podman machine list` and runs `podman machine start` when needed (waits, exit 3 with a message when Podman is missing), runs `postgres:17` (the image tag of `eng/run-docker-tests.ps1`: read it, do not invent one) detached with `-c max_connections=600`, waits for `pg_isready` through `podman exec`, loads `tools/visual/postgres-scenario.sql` through `podman exec ... psql`, prints host, port, user, password, database in a table, and writes a profile `pg-visual` (Staging) next to the SQLite ones in `<Dir>/profiles.json` when `-Dir` is given (same JSON as `New-VisualProfile.ps1`; ids deterministic). `Stop`: removes the container (`podman rm -f`) and prints `podman ps -a` count. Re-runnable.
- `postgres-scenario.sql`: schemas `ventes` and `archive`; the tables of `seed.sql` translated to PostgreSQL (`clients`, `commandes`, `lignes_commande`, `factures`, `produits`, `stocks`, a view) with French sample rows including NULLs; roles `app_read`, `app_write` (member of `app_read`), `report`, a role named `user` quoted (P19-01), a group role with exactly one member (singular `1 membre`, P19-18); grants that make every one of the 7 privilege columns show a mix of granted / not granted / partial; 450 empty tables `bulk_001` ... `bulk_450` in schema `archive` (rows-cut notice of the privilege matrix, P19-04); a function and a procedure; a table with a foreign key to another for the plan.
- Blocking and many sessions are not SQL: `tools/visual/New-PostgresSessions.ps1 -Port <p> -Count 450 -Block` opens `Count` idle sessions with `psql` in the background (`application_name = squill-visual`), plus a transaction holding `update ventes.clients ...` and a second session waiting on the same row (a blocking chain of depth 2), prints the process ids and a `-Stop` switch that kills them. Uses the container's `psql` through `podman exec -d`, so no PostgreSQL client is required on the host.
- Update `docs/verification/new-screens.md` "How to prepare" (PostgreSQL bullet) and `tools/visual/README.md` with the two scripts. No checklist row, no tick.

## Out of scope
- MySQL / SQL Server scenarios (the Docker-gated suites cover them), changing the app.

## Tasks
- [ ] Scripts and SQL, README and "How to prepare" lines.
- [ ] Run `Start` twice (second run recreates), `psql` through `podman exec` counts: 450 `bulk_*` tables, the 4 roles, the view; `New-PostgresSessions.ps1 -Count 450 -Block` then `select count(*) from pg_stat_activity` >= 450 and a `pg_locks` wait exists; `-Stop`, `Stop`, `podman ps -a` empty.
- [ ] Connect the Avalonia or WinUI head to it once (profile `pg-visual`): the explorer lists `ventes` and `archive`.

## Acceptance criteria
- Docs/tools only: no build needed unless C# changed; links resolve; container and sessions removed; Done notes give the three commands to start, fill and stop.

## Risks
- `max_connections=600` and 450 sessions need memory on the Podman machine; if the machine kills the container, lower `-Count` to 420 (the notice needs only more than 400) and say so.
- On first start `podman machine start` can take a minute: wait with a timeout, never hang.

## Suggested commit message
Prepare a PostgreSQL container with the data of the verification passes
