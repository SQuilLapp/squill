# P11-35 - SSH tunnel proven against real servers

Status: done
Depends on: P11-02, P11-03, P11-04

## Goal
The SSH tunnel (P2-05, `SshNetTunnelFactory`) and `SessionManager` are exercised end to end: a database reachable only through a jump host opens, queries and closes cleanly. They were verified by publish and startup only.

## Design decisions (final)
- Docker-gated tests in `tests/Squill.Platform.Tests` and `tests/Squill.Presentation.Tests` (add `DockerGate` copies as in the driver projects; Testcontainers package reference only if not present: it is a NuGet already named in `docs/architecture.md`, version from `Directory.Packages.props`): a Testcontainers network with an `lscr.io/linuxserver/openssh-server` container (password auth and a key pair generated in the test, `PERMIT_TCP_FORWARDING`/`GatewayPorts` as needed: `DOCKER_MODS` not allowed, use the environment variables the image documents and record them in the test), and a database container **without a published port** on the same network. The tunnel forwards to the database's network alias.
- Cases: password auth and key auth (with and without passphrase) open a tunnel and `select 1` succeeds through `SessionManager.OpenAsync` for PostgreSQL (the other engines run the same test as `[Theory]` over the three images if the runtime cost stays under 3 minutes; otherwise PostgreSQL and SQL Server); wrong password -> error with the SSH message and nothing left open; wrong host key policy: check what `SshNetTunnelFactory` does about the server host key today (accepts anything?): record it in the report and, if it accepts silently, add a trust-on-first-use prompt card to the report as a finding (do not implement here); dropping the SSH container while connected -> the next query fails with a message and the session can be reopened; `DisposeAsync` closes the local listener (port free afterwards).
- Windows Podman caveat: the SSH container port is published to the host as usual; document the networking note in `docs/testing.md`.

## Out of scope
- Jump chains, agent forwarding, UI.

## Tasks
- [x] Tests, fixes, finding report.

## Report
- Host key policy: `SshNetTunnelFactory` accepts any server host key silently and persists nothing (`HostKeyReceived` sets `CanTrust = true`; its own `shortcut:` comment says so). Finding: a card is needed for trust-on-first-use (known-hosts store, prompt on first connection, hard failure with a clear message when the key changes). Not implemented here.
- Wrong password gives the SSH message `Permission denied (password).` and `SessionManager` leaves no session open.
- Dropping the jump host: the next query fails with a message, the session closes and a new one opens once the host is back.
- Only PostgreSQL runs behind the jump host (the tunnel is engine independent); MySQL and SQL Server were not added.
- No production code needed a fix.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` 0 warnings; default test run green; `rtk pwsh eng/run-docker-tests.ps1 -Engine all` green with the new tests included; no container or network left (`podman network ls` shows no Testcontainers network).

## Suggested commit message
Test the SSH tunnel and session opening against real servers
