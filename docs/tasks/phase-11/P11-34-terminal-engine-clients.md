# P11-34 - Terminal: native client launch per engine

Status: done
Depends on: P11-06

## Goal
The terminal tab launches the right command-line client for each engine with the profile's host, port, database and user, never puts the password on a command line, and says clearly when the client is not installed. Read `TerminalLauncher`, `TerminalCommandLine`, `TerminalLaunch` first: they already exist (P6-01); this card checks and completes them.

## Design decisions (final)
- Expected command per dialect (final, tests assert the exact `TerminalLaunch`): PostgreSQL `psql -h host -p port -U user -d database` with env `PGPASSWORD`; MySQL `mysql -h host -P port -u user -D database` with env `MYSQL_PWD` (and `--ssl-mode` from `TlsMode` once P11-06 is in); SQL Server `sqlcmd -S host,port -U user -d database` with env `SQLCMDPASSWORD`, `-C` when the profile trusts the certificate; SQLite `sqlite3 "path"`. With an SSH tunnel the host is `127.0.0.1` and the port is the tunnel's local port (the tunnel stays open while the tab lives and is closed with it).
- Client discovery: PATH lookup plus the default install folders on Windows (`C:\Program Files\PostgreSQL\*\bin`, `C:\Program Files\MySQL\*\bin`, `C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\*\Tools\Binn`); when missing, the tab shows a catalog message "psql was not found" with the install hint (fr/en) instead of an empty terminal.
- Quoting of paths and names with spaces and quotes, tested per dialect.
- The password is read from the secret store only at launch and passed through the environment of the child process; a test asserts the command line text never contains it.

## Out of scope
- Embedded SQL shell inside SQuilL, new terminal features.

## Tasks
- [x] Tests for the 4 launches (with and without SSH, with spaces in names), discovery, missing-client message, fixes.
- [x] If a client exists on the machine (`sqlite3`, `psql`...), launch it once headless with `--version` through the same launcher code path as a smoke test; skip silently with a printed reason otherwise (never fail on a missing client).

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` 0 warnings; `rtk bypass dotnet test --solution Squill.slnx -c Release` green.
- To check by hand: open a terminal tab from a PostgreSQL profile with a client installed and from one without (message shown).

## Suggested commit message
Launch the native client of each engine from the terminal tab
