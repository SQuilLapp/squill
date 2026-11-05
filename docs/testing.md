# Testing with containers

The tests of the PostgreSQL, MySQL and SQL Server drivers use Testcontainers and are skipped unless `SQUILL_DOCKER_TESTS=1` is set. On a machine with Podman and no Docker, run them with one command.

## Prerequisites

- PowerShell 7 and the .NET SDK from `global.json`.
- Podman 5.x with a machine (`podman machine init` once). The script starts the machine when it is stopped.
- Machine memory: 2048 MiB is enough for PostgreSQL and MySQL; SQL Server needs 4096 MiB. On a WSL machine (the Windows default) the script does not check it: the VM gets its memory from WSL (`.wslconfig`, half of the host RAM by default) and `podman machine set --memory` is refused. On a Hyper-V, QEMU or other machine the script checks it and stops below 4096; the one-time command (the script never changes the machine itself) is:

```bash
podman machine stop; podman machine set --memory 4096; podman machine start
```

## Commands

```bash
pwsh eng/run-docker-tests.ps1 -Engine postgres
pwsh eng/run-docker-tests.ps1 -Engine mysql
pwsh eng/run-docker-tests.ps1 -Engine sqlserver
pwsh eng/run-docker-tests.ps1 -Engine ssh           # SSH tunnel and SessionManager against an OpenSSH container (PostgreSQL behind it)
pwsh eng/run-docker-tests.ps1                      # postgres, mysql, sqlserver and ssh
pwsh eng/run-docker-tests.ps1 -Engine postgres -Filter '--filter-class *PostgresConnectionStringTests'
```

`-Filter` is split on spaces and passed to the xunit v3 test application (`--filter-class`, `--filter-method`, ...). The exit code is the one of `dotnet test`. For the child process only, the script sets `DOCKER_HOST` (kept if already set), `TESTCONTAINERS_RYUK_DISABLED=true` and `SQUILL_DOCKER_TESTS=1`. In a `finally` it removes the containers labelled `org.testcontainers=true`.

`DOCKER_HOST` value that works with Podman 5.6.2 on WSL (machine `podman-machine-default`): `npipe://./pipe/podman-machine-default`. This is the pipe printed by `podman machine inspect` (`\.\pipe\podman-machine-default`) in the form Docker.DotNet accepts; the four-slash form `npipe:////./pipe/...` that `podman machine start` prints is rejected ("The endpoint is not a npipe URI").

## Keeping a container for hand checks

```bash
pwsh eng/run-docker-tests.ps1 -Engine postgres -Keep    # or mysql, sqlserver
```

No test runs. The script starts the engine's container (`postgres:17-alpine`, `mysql:8.4`, SQL Server 2022) with a published loopback port, loads `tests/Shared/SampleSchema/<engine>.sql`, prints host, port, user, password and database, and waits for Enter; it then removes the container (also when interrupted). Use the printed values in a SQuilL connection. The hand checklists are in `docs/verification/`.

## SSH tunnel tests

`-Engine ssh` runs `SshJumpHostTests` (`tests/Squill.Platform.Tests`, the tunnel alone) and `SshSessionTests` (`tests/Squill.Presentation.Tests`, `SessionManager` with the real tunnel and the PostgreSQL provider). Both share `tests/Shared/SshJumpFixture.cs`: a Testcontainers network, a `postgres:17-alpine` container on it with no published port (alias `db`) and a `lscr.io/linuxserver/openssh-server` container (alias `jump`) whose port 2222 is published to the host as for any other container. The tunnel connects to the published port and forwards to `db:5432`, which only the jump host can resolve.

- The image forbids TCP forwarding (`AllowTcpForwarding no`) and has no variable for it, so the fixture copies a `sshd_config.d/forward.conf` into the container, which the image includes when that folder exists. No Docker mod is used.
- Password login uses `USER_NAME`, `USER_PASSWORD`, `PASSWORD_ACCESS=true`; the two ed25519 keys (with and without passphrase) are made by `ssh-keygen` inside the container and appended to `authorized_keys`.
- Podman on Windows: the host reaches the published port as usual; no extra networking is needed. Stopping and starting the jump host container may change its published port, so the tests read it again after a restart.
- Only PostgreSQL runs behind the jump host: the tunnel code is engine independent (a loopback port), and one engine keeps the run near 20 s.

## Troubleshooting

- **Pipe not found / endpoint is not a npipe URI**: the machine is stopped (`podman machine start`) or `DOCKER_HOST` is set to a wrong value in your shell; unset it so the script computes it.
- **Image pull timeouts**: the first run pulls `postgres:17-alpine`, `mysql:8.4`, `mcr.microsoft.com/mssql/server:2022-latest` or `lscr.io/linuxserver/openssh-server:latest`; pull it once by hand (`podman pull <image>`) and retry.
- **Ryuk is disabled** because its container needs the Docker socket mounted, which does not work through the Podman pipe. The script's `finally` block does the cleanup instead; if a run is killed, remove leftovers with `podman rm -f $(podman ps -aq --filter label=org.testcontainers=true)`.

## CI

CI runs on Linux with real Docker and keeps Ryuk enabled. In the `linux` job of `.github/workflows/ci.yml`, the full-solution test step runs with the gate off; a later step pulls the three images, then runs the PostgreSQL, MySQL and SQL Server test projects and the SSH tunnel test classes with `SQUILL_DOCKER_TESTS=1`. This script is only for Podman workstations.
