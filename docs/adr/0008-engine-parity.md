# ADR 0008 - Engine parity (SQLite, PostgreSQL, MySQL, SQL Server)

- Status: accepted
- Date: 2026-11-05

## Context
Before phase 11 only SQLite had been exercised end to end. The three server engines were verified by Native AOT publish and startup only ([AOT driver report](../poc/aot-drivers.md)); their DML, schema reading, designer and editor features had never run against a real server. Phase 11 (cards P11-01 to P11-40, [task index](../tasks/README.md)) brings the four engines to the same feature level and proves it against real servers. The per-engine evidence is in the four reports of [docs/verification/](../verification/sqlite.md): [SQLite](../verification/sqlite.md), [PostgreSQL](../verification/postgres.md), [MySQL](../verification/mysql.md), [SQL Server](../verification/sqlserver.md).

## Decisions
- **Podman runner and CI containers.** `eng/run-docker-tests.ps1` runs the Docker-gated tests (Testcontainers) on a developer machine with Podman and no Docker (`-Engine`, `-Filter`, `-Keep` to leave a container for hand checks); CI runs the same tests on Linux with real Docker. Prerequisites and troubleshooting: [docs/testing.md](../testing.md).
- **Connection options.** `ConnectionProfile` gets `TlsMode` (`Default`, `Disable`, `Require`, `VerifyFull`), `TrustServerCertificate`, connect and command timeouts. Defaults keep older profile files loading unchanged. Each provider maps them to its driver; cancel and timeout are proven on every engine.
- **Multiple result sets and server messages.** `IResultReader` exposes `NextResultAsync` and `Messages`; a statement that returns several sets shows one result tab per set plus a Messages tab (`PRINT`, `RAISE NOTICE`, MySQL notes). `GO` lines of a SQL Server script are batch separators and are not sent.
- **Schema objects.** `SchemaSnapshot` carries routines (functions, procedures), triggers, view definitions and PostgreSQL materialized views; the explorer lists them and opens a definition script. SQLite has triggers and views but no routines.
- **Database listing.** `IDatabaseConnection.ListDatabasesAsync` lists the databases of a server (SQLite: the one file); a connection can browse another database without `USE`. For MySQL a database is a schema.
- **Column type policy.** Every type is read and shown, and written back where editable; values the AOT-safe drivers cannot map show as `<unsupported ...>` and are read-only. The full table is [docs/column-types.md](../column-types.md).
- **Bugs found by the real servers** (each fixed with a regression test): rows-affected text for DML (all engines), SQLite cancel blocking the UI thread, PostgreSQL `@name` parameters, mixed-case and quoted identifier completion on PostgreSQL, bracket and backtick quoting in completion.
- **Terminal and SSH.** The terminal launches the native client per engine (`psql`, `mysql`, `sqlcmd`, `sqlite3`) with the password passed through the environment only; SSH tunnels are tested against real servers behind a jump host (P11-35).

## Support matrix
Source: the automated sections of the four verification reports. `full` = covered by tests that ran against the engine (server engines: a real container) through the view models. The manual window checklists are **not done yet**, so nothing below has been checked by hand in a window.

| Feature | SQLite | PostgreSQL | MySQL | SQL Server |
|---|---|---|---|---|
| Connect, TLS options, timeouts, cancel | full (TLS n/a) | full | full | full |
| Run statements, run under caret, parameters | full (`:name`; `?` refused) | full (`@name`; `$1` refused) | full (`@name`; `?` refused) | full (`@name`) |
| Several result sets and messages | partial (one set per statement) | full | full | full |
| Result grid, 100,000 rows, CSV and JSON export | full | full | full | full |
| Grid editing and commit | full | full | partial (no transaction on engines without them) | full (identity read-back through `OUTPUT`) |
| Table designer: create | full | full | full | full |
| Table designer: alter | full (by table rebuild) | full | partial (DDL cannot be rolled back) | partial (adding an identity to a column is refused) |
| ER diagram | full | full | full | full |
| Explorer: tables, views, indexes, foreign keys | full | full | full | full |
| Routines | no (SQLite has none) | full | full | full |
| Triggers and view definitions | full | full | full | full |
| Materialized views | no (none in SQLite) | full | no (none in MySQL) | no (not listed apart) |
| Browse other databases | partial (a file is one database) | full | full | full |
| Completion, hover, diagnostics, formatter | full | full | full | full |
| Column types read and written | full | partial (enum and interval with months unreadable, see column types) | full | full (`rowversion` read-only) |
| Native terminal client | full (`sqlite3`) | full (`psql`) | full (`mysql`) | full (`sqlcmd`) |
| SSH tunnel | n/a | full (jump host test) | see [P11-35](../tasks/phase-11/P11-35-ssh-tunnel-containers.md) | see [P11-35](../tasks/phase-11/P11-35-ssh-tunnel-containers.md) |
| Manual check in the WinUI window | pending | pending | pending | pending |

## Not supported
Sequences, user-defined types, synonyms, events (MySQL), spatial types, Windows authentication (SQL Server), MariaDB (not tested, not a goal), Azure SQL. Oracle exists only as a proof of concept through a bridge process ([docs/poc/oracle-bridge.md](../poc/oracle-bridge.md)).

## Avalonia head
Still on AvaloniaEdit as its editor, see [ADR 0002](0002-editor-and-terminal-components.md). Phase 11 added the same features to both heads (connection options, result tabs, explorer objects), but the checklists give Avalonia only a smoke pass and **nothing of it has been driven by hand yet**.

## Consequences
- The four engines share one tested feature set; a regression in a shared layer shows in four container suites.
- Container tests need Podman or Docker, and about 4 GB of VM memory for SQL Server.

## Open items
- The manual checklists in section 3 of [sqlite.md](../verification/sqlite.md), [postgres.md](../verification/postgres.md), [mysql.md](../verification/mysql.md) and [sqlserver.md](../verification/sqlserver.md) are unticked: the owner does them in the WinUI window (Avalonia smoke rows included). Cards P11-36 to P11-39 stay "automated done, manual pending" until then.
- Any row that fails there becomes a card `P11-41` onwards. None had been created when this ADR was written.
- Limitations carried over: PostgreSQL enum and interval-with-months cells are not readable under the AOT-safe data source; MySQL DDL cannot be rolled back; the SQL Server designer cannot add an identity to an existing column.
