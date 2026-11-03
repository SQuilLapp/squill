# P11-01 - Podman runner for the Docker-gated tests

Status: todo
Depends on: none

## Goal
The Testcontainers tests gated by `SQUILL_DOCKER_TESTS=1` (Postgres, MySQL, SQL Server) can be started by one command on a machine that has Podman 5.x and no Docker.

## Design decisions (final)
- Script `eng/run-docker-tests.ps1` (PowerShell 7), parameters `-Engine postgres|mysql|sqlserver|all` (default `all`) and `-Filter <dotnet test filter>`; it runs `dotnet test --project tests/Squill.Data.<Engine>.Tests -c Release` for the chosen engines (check the exact CLI of the .NET 11 RC3 test runner with `dotnet test --help`; the solution form is `--solution`).
- The script: finds `podman` (stop with a clear message if absent), starts the machine when stopped (`podman machine start`), reads its memory (`podman machine inspect`) and, for `sqlserver`/`all`, stops with the exact command `podman machine stop; podman machine set --memory 4096; podman machine start` when the memory is below 4096 MiB (it does not change the machine by itself), sets `DOCKER_HOST` to the pipe printed by `podman machine inspect` (the npipe form Testcontainers accepts; verify and document the exact string), `TESTCONTAINERS_RYUK_DISABLED=true` and `SQUILL_DOCKER_TESTS=1` for the process only, runs the tests, and in a `finally` removes leftover containers labelled `org.testcontainers=true` (`podman ps -aq --filter label=org.testcontainers=true` then `podman rm -f`).
- The exit code is the test exit code. No other script, no change to the test projects in this card.
- Document in `docs/testing.md` (new): prerequisites, the one-time memory command, the commands, troubleshooting (pipe not found, image pull timeouts, Ryuk disabled and why), and a note that CI uses real Docker (P11-05). Link it from `README.md` in the section that mentions `SQUILL_DOCKER_TESTS`.

## Out of scope
- Making the tests pass (P11-02 ... P11-04). Docker Desktop support beyond not breaking it (if `DOCKER_HOST` is already set the script keeps it).

## Tasks
- [ ] Write the script and `docs/testing.md`.
- [ ] Run it for `postgres` with the existing tests: the container must start and the tests must at least begin to execute (pass or fail is P11-02). Record the exact `DOCKER_HOST` value that worked in `docs/testing.md`.
- [ ] Run it with `-Filter` on a connection-string test to prove the exit code is forwarded.

## Acceptance criteria
- `rtk pwsh eng/run-docker-tests.ps1 -Engine postgres` pulls the image, starts a container, runs the tests and leaves no container (`podman ps -a` empty).
- Without Podman on PATH the script exits non-zero with a readable message (check by running it with an emptied `$env:PATH`).
- `rtk dotnet build Squill.slnx -c Release -warnaserror` is unaffected (0 warnings).
- To check by hand (owner): none; the report states the Podman machine memory used.

## Suggested commit message
Add a Podman runner script and docs for the Docker-gated tests
