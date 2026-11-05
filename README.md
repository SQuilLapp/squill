# SQuilL

SQuilL is a SQL client built on .NET 11 (RC3) and Native AOT. The Windows head uses WinUI 3, the macOS/Linux head uses Avalonia. Both share one view-model layer (`Squill.Presentation`).

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

SSH tunnels (`SSH.NET`) are compatible under the same conditions (publish and startup only).

Phase 11 ran the four engines against real servers (Testcontainers). The feature-by-engine support matrix, what is not supported (MariaDB, Azure SQL, sequences, user-defined types, ...) and the manual window checks still pending are in [ADR 0008](docs/adr/0008-engine-parity.md). Tests and how to run them: [docs/testing.md](docs/testing.md).

## Schema designer and ER diagram

Open a table in the designer to create or alter columns, indexes and foreign keys: the generated DDL is previewed before it runs. The ER diagram shows the tables of a schema with their foreign keys, can be exported to SVG, and remembers box positions per connection. Both work on SQLite, PostgreSQL, MySQL / MariaDB and SQL Server.

- SQLite changes that `ALTER TABLE` cannot express are done by rebuilding the table inside a transaction (foreign keys are checked afterwards).
- MySQL / MariaDB does not roll back DDL: if a statement fails, the earlier ones stay applied, and the designer warns about it.

See [ADR 0004](docs/adr/0004-schema-designer.md) for the design and what is not covered.

## Editing data

Results of a plain `SELECT` over one table can be edited in the grid: change cells, add rows, delete rows, then commit or discard. The primary key must be selected; otherwise the result is read-only and the panel says why (join, view, `DISTINCT` / `GROUP BY` / `WITH`, no primary key, key column not selected). The generated `INSERT`, `UPDATE` and `DELETE` statements can be previewed before they run, and all changes are applied in one transaction (MySQL / MariaDB engines without transactions excepted).

- Updates and deletes match the primary key only: if someone else changed other columns of the row meanwhile, your commit overwrites them (last write wins). A row deleted by someone else makes the commit fail and roll back.
- The DML tests for PostgreSQL, MySQL / MariaDB and SQL Server need Docker and have never been run, and the edit flow was not driven by hand in either head.

See [ADR 0005](docs/adr/0005-grid-editing.md) for the design and what is not covered.

## Build and test

Prerequisite: the .NET SDK pinned in [global.json](global.json) (11.0.100-rc.3).

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

`eng/pack-windows.ps1` publishes the WinUI head with Native AOT and packs it with Velopack (`dotnet tool restore` installs `vpk`) into `artifacts/releases`:

```powershell
./eng/pack-windows.ps1 -Version 0.0.1-test
```

The `release` GitHub workflow (`.github/workflows/release.yml`) runs the same script.

### LoomTerminal

Both heads get their terminal tabs from the [`LoomTerminal.WinUI`](https://www.nuget.org/packages/LoomTerminal.WinUI) and [`LoomTerminal.Avalonia`](https://www.nuget.org/packages/LoomTerminal.Avalonia) NuGet packages (see [ADR 0002](docs/adr/0002-editor-and-terminal-components.md)); nothing to configure. The WinUI head targets `net11.0-windows10.0.26100.0`.

## Project layout

Dependency graph: `Core` <- `Data` <- `Data.<Driver>`; `Core` <- `Sql`; `Core` <- `Platform`; `Presentation` -> Core, Data, Sql, Platform; heads -> Presentation + providers.

| Project | Role |
|---|---|
| `src/Squill.Core` | Dialects, connection profile, result and schema models |
| `src/Squill.Data` | Provider contracts (`IDatabaseProvider`, connections, result readers) and registry |
| `src/Squill.Data.Sqlite`, `.Postgres`, `.MySql`, `.SqlServer` | One provider per database (execute, stream, read schema) |
| `src/Squill.Sql` | Per-dialect tokenizers, hand-written parser, completion, diagnostics, hover, signature help, formatter, highlighter |
| `src/Squill.Platform` | Secret stores (DPAPI, libsecret, Keychain), app paths, profile/history/snippet/settings stores, SSH tunnel |
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
- ADRs: [0001 target framework](docs/adr/0001-target-framework.md), [0002 editor and terminal components](docs/adr/0002-editor-and-terminal-components.md), [0003 SQL parser](docs/adr/0003-sql-parser.md), [0004 schema designer](docs/adr/0004-schema-designer.md), [0005 grid editing](docs/adr/0005-grid-editing.md), [0006 visual conformance](docs/adr/0006-visual-conformance.md), [0007 localization](docs/adr/0007-localization.md), [0008 engine parity](docs/adr/0008-engine-parity.md)
- Proofs of concept: [AOT drivers](docs/poc/aot-drivers.md), [grid](docs/poc/grid.md), [editor](docs/poc/editor.md)
- [Task cards](docs/tasks/README.md)

## License

[MIT](LICENSE)
