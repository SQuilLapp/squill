# SQuilL

SQuilL is a SQL client built on .NET 11 (RC3) and Native AOT. The Windows head uses WinUI 3, the macOS/Linux head uses Avalonia. Both share one view-model layer (`Squill.Presentation`).

## Supported databases

| Database | Driver | Native AOT verdict (see [docs/poc/aot-drivers.md](docs/poc/aot-drivers.md)) |
|---|---|---|
| SQLite | `Microsoft.Data.Sqlite` | compatible (publish and run); `e_sqlite3.dll` must ship next to the exe |
| PostgreSQL | `Npgsql` (`NpgsqlSlimDataSourceBuilder`) | compatible (publish only, not run against a server) |
| MySQL / MariaDB | `MySqlConnector` | compatible (publish only, not run against a server) |
| SQL Server | `Microsoft.Data.SqlClient` | compatible (publish and startup only); the package raises IL2104/IL3053 itself |

SSH tunnels (`SSH.NET`) are compatible under the same conditions (publish and startup only).

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

On PowerShell use `$env:SQUILL_DOCKER_TESTS = '1'` before the command.

Native AOT publish of a head (zero IL2xxx/IL3xxx warnings expected):

```bash
dotnet publish src/Squill.App.WinUI -c Release -r win-x64
dotnet publish src/Squill.App.Avalonia -c Release -r win-x64
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

### Optional LoomEditor and LoomTerminal

SQuilL can use the author's [LoomEditor](docs/adr/0002-editor-and-terminal-components.md) (SQL editor, WinUI head) and LoomTerminal (terminal tabs, both heads) from local checkouts. Create the git-ignored `Directory.Build.local.props` at the repository root:

```xml
<Project>
  <PropertyGroup>
    <LoomEditorPath>D:\LoomEditor</LoomEditorPath>
    <LoomTerminalPath>D:\LoomTerminal</LoomTerminalPath>
  </PropertyGroup>
</Project>
```

Either property can be omitted. Without `LoomEditorPath` the WinUI head uses a plain-text editor (the Avalonia head uses AvaloniaEdit); without `LoomTerminalPath` no terminal UI is compiled. CI builds without them. With either property set, the WinUI head targets `net11.0-windows10.0.26100.0`.

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
- ADRs: [0001 target framework](docs/adr/0001-target-framework.md), [0002 editor and terminal components](docs/adr/0002-editor-and-terminal-components.md), [0003 SQL parser](docs/adr/0003-sql-parser.md)
- Proofs of concept: [AOT drivers](docs/poc/aot-drivers.md), [grid](docs/poc/grid.md), [editor](docs/poc/editor.md)
- [Task cards](docs/tasks/README.md)

## License

[MIT](LICENSE)
