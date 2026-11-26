# SQuilL

SQuilL is a SQL client built on .NET 11 and Native AOT. The Windows head uses WinUI 3, the macOS/Linux head uses Avalonia. Both share one view-model layer (`Squill.Presentation`).

## Install

Download `SQuilL-stable-x64-Setup.exe` (or `SQuilL-stable-arm64-Setup.exe` when the release has it) from the [latest release](https://github.com/squillapp/squill/releases/latest) and run it: SQuilL installs for the current user in `%LOCALAPPDATA%\SQuilL` and keeps your data in `%APPDATA%\SQuilL`, also after an update or an uninstall. While the installer is unsigned Windows SmartScreen asks for a confirmation (*More info*, *Run anyway*).

Updates: nothing is checked until you choose a frequency (the first run asks once, Settings changes it); a banner offers a new version, *Download* fetches it in the background and *Restart to update* applies it, never without your click. The channel is Stable or Beta. Details, privacy and the release procedure: [docs/release.md](docs/release.md).

## Language and screens

The interface is French or English (Settings, applied after a restart; `SQUILL_LANGUAGE=fr|en|system` overrides it for one run). Phase 9 restyled both heads after the owner's mockups; per-screen status and known gaps are in [ADR 0006](docs/adr/0006-visual-conformance.md) and [docs/design/visual-spec.md](docs/design/visual-spec.md). No screenshots are included yet; capture them with the tools in [tools/visual](tools/visual/README.md).

To add a UI string, add the same key to `UiTextCatalogFr` and `UiTextCatalogEn` (`src/Squill.Presentation/Localization`) and reference it with `{loc:Text}` / `Loc` or `UiText`; the Presentation tests fail on a missing key, an unused key or a hard-coded literal. Decisions: [ADR 0007](docs/adr/0007-localization.md).

## Supported databases

| Database | Driver | Native AOT verdict (see [docs/poc/aot-drivers.md](docs/poc/aot-drivers.md)) |
|---|---|---|
| SQLite | `Microsoft.Data.Sqlite` | compatible (publish and run); `e_sqlite3.dll` must ship next to the exe |
| PostgreSQL | `Npgsql` (`NpgsqlSlimDataSourceBuilder`) | compatible (publish only, not run against a server) |
| MySQL | `MySqlConnector` | compatible (publish only, not run against a server) |
| SQL Server | `Microsoft.Data.SqlClient` | compatible (publish and startup only); the package raises IL2104/IL3053 itself |

SSH tunnels (`SSH.NET`) are compatible under the same conditions (publish and startup only). The jump host key is checked against `known-hosts.json` (`IKnownHostsStore`): the first key is shown with its fingerprint and remembered on your yes, a changed key is refused ([ADR 0013](docs/adr/0013-ssh-host-keys.md)).

Oracle exists only as a proof of concept through a separate bridge process ([docs/poc/oracle-bridge.md](docs/poc/oracle-bridge.md)). Phase 11 ran the four engines against real servers (Testcontainers). The feature-by-engine support matrix, what is not supported (MariaDB, Azure SQL, sequences, user-defined types, ...) and the manual window checks still pending are in [ADR 0008](docs/adr/0008-engine-parity.md). Tests and how to run them: [docs/testing.md](docs/testing.md).

SQL completion (phase 14): after `JOIN` the popup offers each table with its alias and the `ON` condition built from the foreign keys, inserts schema-qualified names where needed, follows the keyword-case setting, and has statement snippets (`sel`, `ins`, `upd`, `del`, `cte`, `crt`). Decisions and limits: [ADR 0011](docs/adr/0011-sql-completion.md).

## Execution plan

`Expliquer` on the query toolbar opens a plan tab (`<name> · Plan`) for the statement under the caret with a tree, a graph and the raw text, relative cost, row estimates and, on SQL Server, the engine's own missing-index suggestion. `Expliquer` never runs the statement. `Analyser` runs it and shows real rows and timings, for read-only statements only (in a rolled-back transaction where the engine allows it); on a Production connection it asks first.

| Database | `Expliquer` | `Analyser` | Cost / estimate / timing |
|---|---|---|---|
| SQLite | yes (structure only) | no | none: nothing is invented |
| PostgreSQL | yes | yes, `BEGIN READ ONLY` then rollback | cost, estimate, timing, buffers |
| MySQL 8.x | yes | yes (`EXPLAIN ANALYZE`, no wrapper) | cost and estimate (estimated), timing (actual) |
| SQL Server | yes (not with bound parameters) | yes, rolled-back transaction | cost, estimate, timing, missing index |

MariaDB and Oracle are not supported for plans, and plan comparison and export do not exist. See [ADR 0010](docs/adr/0010-execution-plan.md) for the design and known limits; the screens still need the owner's checks of [docs/verification/plan.md](docs/verification/plan.md).

## Transactions and production confirmation

The query toolbar has an `Auto-commit | Manuel` toggle. In `Manuel` the first statement that writes opens a transaction (a `SELECT` never does); the toolbar then shows how many statements are not committed and `Valider` (`Ctrl+Alt+C`) / `Annuler` (`Ctrl+Alt+Z`), and the status bar shows how long it has been open. Closing the last tab, disconnecting, going back to auto-commit and quitting ask what to do with the open transaction. On a connection marked `Production`, every modifying statement, `Valider`, grid commit and designer apply asks first; a destructive statement (`DELETE` / `UPDATE` without `WHERE`, `TRUNCATE`, `DROP`) needs the database name typed, shows the planner's estimate of the rows it will touch, and `Entrée` cancels.

| Engine | Manual transactions | DDL inside a transaction | Rows estimate in the confirmation |
|---|---|---|---|
| SQLite | yes | transactional | no |
| PostgreSQL | yes (an error puts the transaction in the `Failed` state until `Annuler`) | transactional | yes |
| MySQL 8.x | yes | **commits implicitly**: the app asks first | yes |
| SQL Server | yes | transactional | yes |

Oracle keeps auto-commit. See [ADR 0012](docs/adr/0012-transactions-and-production-confirmation.md) for the design and known limits; the windows still need the owner's checks of [docs/verification/transactions.md](docs/verification/transactions.md).

## Auto-refresh of a query tab

The clock button before `Stop` re-runs the last run of a tab every `5 s`, `10 s`, `30 s`, `1 m`, `5 m` or a custom 1 to 3600 seconds (default `Disabled`, per tab, not saved). Only a read-only run that ended without error can refresh. A refresh writes no history, keeps the sort, filters, scroll and column widths, and does not close the completion popup. It pauses while grid edits are pending, a value is being edited, the transaction failed, the session is busy or the tab is hidden, and it stops on the first failed refresh or on `Stop`.

## Dropping several tables, and what phase 20 did not verify

In the explorer, select several tables (Ctrl+click, Shift+click) and use `Supprimer N tables` or `Delete`: the confirmation lists them, referencing tables go first, a table outside the selection that references one blocks the drop (there is no `CASCADE`), and PostgreSQL, SQL Server and SQLite drop all or none (MySQL stops at the first failure). Dropping a single table now follows the same path, including the Production confirmation. Checklist for the owner: [docs/verification/multi-delete.md](docs/verification/multi-delete.md).

Phase 20 drove the windows with `tools/visual/Drive-Window.ps1`, but most passes are partial: English and the light theme are mostly not driven, the WinUI import wizard was seen only on its first step, the Avalonia multi-select was never driven in the published app, and nothing ran on Linux or macOS. The WinUI and Avalonia AOT publishes show only the four known third-party warnings. The full list is in [the task index](docs/tasks/README.md) ("Left open by phase 20") and [ADR 0017](docs/adr/0017-verification-and-multi-delete.md).

## Schema designer and ER diagram

Open a table in the designer to create or alter columns, indexes and foreign keys: the generated DDL is previewed before it runs. The ER diagram shows the tables of a schema with their foreign keys, can be exported to SVG, and remembers box positions per connection. Both work on SQLite, PostgreSQL, MySQL / MariaDB and SQL Server.

- SQLite changes that `ALTER TABLE` cannot express are done by rebuilding the table inside a transaction (foreign keys are checked afterwards).
- MySQL / MariaDB does not roll back DDL: if a statement fails, the earlier ones stay applied, and the designer warns about it.

See [ADR 0004](docs/adr/0004-schema-designer.md) for the design and what is not covered.

## Editing data

Results of a plain `SELECT` over one table can be edited in the grid: change cells, add rows, delete rows, then commit or discard. The primary key must be selected; otherwise the result is read-only and the panel says why (join, view, `DISTINCT` / `GROUP BY` / `WITH`, no primary key, key column not selected). The generated `INSERT`, `UPDATE` and `DELETE` statements can be previewed before they run, and all changes are applied in one transaction (MySQL / MariaDB engines without transactions excepted).

- The cell editor follows the column type, with true / false / NULL and date pickers and a `DEFAULT` choice; a long or JSON value opens a preview pane on the right. The explorer shows only what you choose: a DataGrip-style popup, opened from the `n of m` badge on connection, database and schema rows, checks databases, schemas and tables and accepts wildcard patterns (saved per connection through `ITableFilterStore`).
- Updates and deletes match the primary key only: if someone else changed other columns of the row meanwhile, your commit overwrites them (last write wins). A row deleted by someone else makes the commit fail and roll back.
- The DML tests for PostgreSQL, MySQL / MariaDB and SQL Server need Docker and have never been run, and the edit flow was not driven by hand in either head.

See [ADR 0005](docs/adr/0005-grid-editing.md) for the design and what is not covered.

## Home, compare, sessions, privileges and import

A home page with the recent connections, a structure compare of two databases of the same dialect with an editable migration script, a sessions and locks monitor (PostgreSQL, MySQL, SQL Server), a roles and privileges matrix (PostgreSQL) and a CSV import wizard into an existing table. Every write goes through the statement runner and the production confirmation. Decisions, limits and what is not built: [ADR 0015](docs/adr/0015-remaining-mockups.md); owner checklist: [docs/verification/new-screens.md](docs/verification/new-screens.md).

## Build and test

The version is written once, in [eng/Version.props](eng/Version.props) (`SquillVersion`); the release tag is `v` + that value.
`pwsh eng/get-version.ps1 [-Tag v0.1.0]` prints it and checks a tag against it.

Prerequisite: the .NET SDK pinned in [global.json](global.json) (11.0.100).

```bash
dotnet build Squill.slnx -c Release -warnaserror
dotnet test --solution Squill.slnx -c Release
```

`Squill.slnx` builds everywhere (Windows, Linux, macOS). `Squill.Windows.slnx` adds the WinUI 3 head and its tests and only builds on Windows:

```bash
dotnet build Squill.Windows.slnx -c Release -warnaserror
```

Tests that need Docker (PostgreSQL, MySQL, SQL Server containers through Testcontainers) are skipped unless `SQUILL_DOCKER_TESTS=1` is set:

```bash
SQUILL_DOCKER_TESTS=1 dotnet test --solution Squill.slnx -c Release
```

On PowerShell use `$env:SQUILL_DOCKER_TESTS = '1'` before the command. With Podman and no Docker, `pwsh eng/run-docker-tests.ps1` does it all; see [docs/testing.md](docs/testing.md).

Native AOT publish of a head (zero IL2xxx/IL3xxx warnings expected):

```bash
dotnet publish src/Squill.App.WinUI -c Release -r win-x64
dotnet publish src/Squill.App.Avalonia -c Release -r win-x64
```

Run a head from its publish folder (`-o`), not from `bin/.../native/`: that folder holds the raw compiler output without the Windows App SDK DLLs and `.pri` files, so the exe fails at startup with `DllNotFoundException: Microsoft.WindowsAppRuntime.dll`.

```bash
dotnet publish src/Squill.App.WinUI -c Release -r win-x64 -o artifacts/winui
artifacts/winui/Squill.App.WinUI.exe
```

Design tokens (`tokens/squill.tokens.json`) are turned into XAML/AXAML by `tools/Squill.TokenGen`:

```bash
dotnet run --project tools/Squill.TokenGen -c Release -- tokens/squill.tokens.json <output folder>
```

### Windows package

`eng/pack-windows.ps1` is the release build entry point: it publishes the WinUI head with Native AOT and packs it with Velopack (`dotnet tool restore` installs `vpk`) into `artifacts/releases`. The version comes from `eng/Version.props`, the notes from the matching section of `CHANGELOG.md` (a local pack of the current version needs that section), the channel from the version (`stable-x64`, or `beta-x64` for a `-beta.N` version; `-AlsoBeta` packs a stable version for both).

```powershell
./eng/pack-windows.ps1                       # version of eng/Version.props
./eng/pack-windows.ps1 -Version 0.0.1-test -AllowVersionOverride   # local check, Unreleased notes
./eng/pack-windows.ps1 -FromGithub -AlsoBeta # also downloads the previous release so vpk builds deltas
```

The `release` GitHub workflow (`.github/workflows/release.yml`) runs the same script.

### LoomTerminal

Both heads get their terminal tabs from the [`LoomTerminal.WinUI`](https://www.nuget.org/packages/LoomTerminal.WinUI) and [`LoomTerminal.Avalonia`](https://www.nuget.org/packages/LoomTerminal.Avalonia) NuGet packages (see [ADR 0002](docs/adr/0002-editor-and-terminal-components.md)); nothing to configure. The WinUI head targets `net11.0-windows10.0.26100.0`.

## Project layout

Dependency graph: `Core` <- `Data` <- `Data.<Driver>`; `Core` <- `Sql`; `Core` <- `Platform`; `Presentation` -> Core, Data, Sql, Platform; heads -> Presentation + providers.

| Project | Role |
|---|---|
| `src/Squill.Core` | Dialects, connection profile, result and schema models |
| `src/Squill.Data` | Provider contracts (`IDatabaseProvider`, connections, result readers, optional `IPlanReader`) and registry |
| `src/Squill.Data.Sqlite`, `.Postgres`, `.MySql`, `.SqlServer` | One provider per database (execute, stream, read schema) |
| `src/Squill.Sql` | Per-dialect tokenizers, hand-written parser, completion, diagnostics, hover, signature help, formatter, highlighter |
| `src/Squill.Platform` | Secret stores (DPAPI, libsecret, Keychain), app paths, profile/history/snippet/settings/table-filter stores, single-instance guard (`SingleInstance`), SSH tunnel and known-hosts store |
| `src/Squill.Presentation` | UI-framework-free view-models, composition root, terminal launcher |
| `src/Squill.App.Shared` | Source shared (linked) by both heads, such as the LoomTerminal host |
| `src/Squill.App.WinUI` | WinUI 3 head (Windows, Win2D grid) |
| `src/Squill.App.Avalonia` | Avalonia head (macOS, Linux, Windows) |
| `src/Squill.DesignSystem` | Design token model and XAML/AXAML generator |
| `tools/Squill.TokenGen` | Command-line wrapper around the token generator |
| `tools/Squill.GrammarGen` | Generates the TextMate grammars in `assets/grammars/` |
| `tools/Squill.AotProbe.*` | One Native AOT console per driver (SQLite, Postgres, MySQL, SQL Server, SSH.NET) |
| `tools/Squill.Bench` | BenchmarkDotNet tokenizer benchmarks |
| `tests/*.Tests` | One xunit.v3 test project per source project |

## Documentation

- [Architecture note and risks](docs/architecture.md)
- [Testing](docs/testing.md), [column types per engine](docs/column-types.md), [verification reports](docs/verification/sqlite.md)
- ADRs: [0001 target framework](docs/adr/0001-target-framework.md), [0002 editor and terminal components](docs/adr/0002-editor-and-terminal-components.md), [0003 SQL parser](docs/adr/0003-sql-parser.md), [0004 schema designer](docs/adr/0004-schema-designer.md), [0005 grid editing](docs/adr/0005-grid-editing.md), [0006 visual conformance](docs/adr/0006-visual-conformance.md), [0007 localization](docs/adr/0007-localization.md), [0008 engine parity](docs/adr/0008-engine-parity.md), [0009 release and auto-update](docs/adr/0009-release-and-auto-update.md), [0010 execution plan](docs/adr/0010-execution-plan.md), [0011 SQL completion](docs/adr/0011-sql-completion.md), [0012 transactions and production confirmation](docs/adr/0012-transactions-and-production-confirmation.md), [0014 grid filter and command palette](docs/adr/0014-grid-filter-and-command-palette.md), [0015 remaining mockups](docs/adr/0015-remaining-mockups.md)
- Proofs of concept: [AOT drivers](docs/poc/aot-drivers.md), [grid](docs/poc/grid.md), [editor](docs/poc/editor.md)
- [Task cards](docs/tasks/README.md)

## License

[MIT](LICENSE)
