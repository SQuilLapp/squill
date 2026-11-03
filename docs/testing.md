# Testing with containers

The tests of the PostgreSQL, MySQL and SQL Server drivers use Testcontainers and are skipped unless `SQUILL_DOCKER_TESTS=1` is set. On a machine with Podman and no Docker, run them with one command.

## Prerequisites

- PowerShell 7 and the .NET SDK from `global.json`.
- Podman 5.x with a machine (`podman machine init` once). The script starts the machine when it is stopped.
- Machine memory: 2048 MiB is enough for PostgreSQL and MySQL; SQL Server needs 4096 MiB. One-time command (the script never changes the machine itself):

```bash
podman machine stop; podman machine set --memory 4096; podman machine start
```

## Commands

```bash
pwsh eng/run-docker-tests.ps1 -Engine postgres
pwsh eng/run-docker-tests.ps1 -Engine mysql
pwsh eng/run-docker-tests.ps1 -Engine sqlserver
pwsh eng/run-docker-tests.ps1                      # all three
pwsh eng/run-docker-tests.ps1 -Engine postgres -Filter '--filter-class *PostgresConnectionStringTests'
```

`-Filter` is split on spaces and passed to the xunit v3 test application (`--filter-class`, `--filter-method`, ...). The exit code is the one of `dotnet test`. For the child process only, the script sets `DOCKER_HOST` (kept if already set), `TESTCONTAINERS_RYUK_DISABLED=true` and `SQUILL_DOCKER_TESTS=1`. In a `finally` it removes the containers labelled `org.testcontainers=true`.

`DOCKER_HOST` value that works with Podman 5.6.2 on WSL (machine `podman-machine-default`): `npipe://./pipe/podman-machine-default`. This is the pipe printed by `podman machine inspect` (`\.\pipe\podman-machine-default`) in the form Docker.DotNet accepts; the four-slash form `npipe:////./pipe/...` that `podman machine start` prints is rejected ("The endpoint is not a npipe URI").

## Troubleshooting

- **Pipe not found / endpoint is not a npipe URI**: the machine is stopped (`podman machine start`) or `DOCKER_HOST` is set to a wrong value in your shell; unset it so the script computes it.
- **Image pull timeouts**: the first run pulls `postgres:17-alpine`, `mysql:8.4` or `mcr.microsoft.com/mssql/server:2022-latest`; pull it once by hand (`podman pull <image>`) and retry.
- **Ryuk is disabled** because its container needs the Docker socket mounted, which does not work through the Podman pipe. The script's `finally` block does the cleanup instead; if a run is killed, remove leftovers with `podman rm -f $(podman ps -aq --filter label=org.testcontainers=true)`.

## CI

CI runs on Linux with real Docker and keeps Ryuk enabled (P11-05); this script is only for Podman workstations.
