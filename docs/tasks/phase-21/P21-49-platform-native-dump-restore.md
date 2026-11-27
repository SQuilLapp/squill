# P21-49 - Platform and Presentation: backup and restore with the engine's own tools, and restore of a SQL file

Status: todo
Depends on: P21-48, P21-30

## Goal
Use `pg_dump` / `pg_restore`, `mysqldump`, the SQLite backup API and SQL Server `BACKUP DATABASE` when available, and restore a `.sql` file through the script runner otherwise.

Exists today: `TerminalLauncher` / `TerminalCommandLine` find native clients and pass the password through the environment only (P11-34); the script run report (P21-30); generated dumps (P21-48).

## Design decisions (final)
- `NativeBackupTools` (Platform): detection on `PATH` (and the PostgreSQL install folders on Windows) with the tool version; a backup or restore runs as a child process with `ArgumentList` (no shell), the password in `PGPASSWORD` / `MYSQL_PWD` environment only, stdout/stderr captured to a log shown at the end, cancellable (kills the process tree).
- Per engine: PostgreSQL `pg_dump -Fc` / `pg_restore` (custom format) or plain SQL; MySQL `mysqldump --single-transaction --routines --triggers`, restore through the script runner (a `mysql` client restore is offered when found); SQLite: `VACUUM INTO '<file>'` (no tool needed), restore = open the file; SQL Server: `BACKUP DATABASE ... TO DISK = '<server path>'` and `RESTORE` statements through the statement runner (the path is on the **server**, said in the UI).
- Over an SSH tunnel, tools connect to the forwarded local port.
- Restore of a `.sql` file: opens it in the script runner with policy `Ask` (P21-30) on the chosen connection; production confirmation applies.
- When no tool is found, the dialog offers the generated dump (P21-48) instead and says why.

## Out of scope
- UI (P21-50), scheduling backups.

## Tasks
- [ ] Command-line building tests per engine; Docker-gated backup and restore round trip for PostgreSQL (tools from the container image run through `podman exec` are **not** used: skip when the tool is not on the host, and say so).

## Acceptance criteria
- Recipe 2 common acceptance.

## Risks
- Tool and server major versions must match for `pg_dump`: check and warn.

## Open points (defaults)
- None.

## Suggested commit message
Back up and restore with the engine tools when available, or restore a SQL file
