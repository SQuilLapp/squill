# SQuilL - Architecture note and risks (v0)

## Starting point (2026-10-29)

- SDK: .NET 11.0.100-rc.3 (target chosen by the project owner, see [ADR 0001](adr/0001-target-framework.md)). Design system and mockups: found (shared canvas), tokens extracted to `tokens/squill.tokens.json`.

## Verified stable versions (NuGet)

| Package | Version | Notes |
|---|---|---|
| Avalonia | 12.1.4 | AOT to validate |
| AvaloniaEdit | 12.0.0 (id `Avalonia.AvaloniaEdit`) | Restores and publishes under Avalonia 12.1.4 AOT; tests use Avalonia.Headless 12.1.4 (Avalonia.Headless.XUnit targets a newer xunit.v3 than 4.0.2) |
| Microsoft.WindowsAppSDK | 2.6.4 | WinUI 3 AOT to prove |
| CommunityToolkit.Mvvm | 8.4.2 | |
| Npgsql | 10.0.3 | `NpgsqlSlimDataSourceBuilder` |
| MySqlConnector | 2.6.2 | |
| Microsoft.Data.SqlClient | 7.1.1 | High AOT risk |
| Microsoft.Data.Sqlite | 11.0.0-rc.3.26522.148 (10.0.14 stable) | Pinned in `Directory.Packages.props`; NuGet only resolves the full RC version string |
| Antlr4.Runtime.Standard | 4.13.1 | |
| SSH.NET | 2026.0.0 | AOT to validate |
| Testcontainers | 4.16.0 | |
| BenchmarkDotNet | 0.15.8 | |
| xunit.v3 | 4.0.2 | |
| Velopack | 1.2.161 | Auto-update candidate |

## Risks and outcomes

| # | Risk | Severity | Outcome |
|---|---|---|---|
| R1 | SQL editor: LoomEditor (WinUI 3) with AvaloniaEdit as interim on Avalonia, see [ADR 0002](adr/0002-editor-and-terminal-components.md) | High | Mitigated: `ISqlEditor` abstraction; completion, hover and markers work on 100,000 lines without a stall after start-up; SQL grammars cannot be registered in LoomEditor yet, so highlighting uses decorations ([editor report](poc/editor.md)) |
| R2 | WinUI 3 + Native AOT (XAML, MSIX, WinRT) | High | Met: the WinUI head publishes with Native AOT win-x64 without IL2xxx/IL3xxx warnings and is packed with Velopack (`eng/pack-windows.ps1`) |
| R3 | Drivers under AOT: SqlClient, Npgsql, MySqlConnector | High | All compatible; Npgsql, MySqlConnector, SqlClient and SSH.NET verified by publish and startup only, no server available ([AOT driver report](poc/aot-drivers.md)) |
| R4 | 1M-row grid | High | Met: custom-drawn virtualized grid, 4.4 ms (Avalonia) and 2.4 ms (WinUI) average frame ([grid report](poc/grid.md)) |
| R5 | Error-tolerant, multi-dialect SQL parser | Medium | Hand-written parser over per-dialect tokenizers, no ANTLR ([ADR 0003](adr/0003-sql-parser.md)) |
| R6 | Cross-platform secret storage | Medium | DPAPI, libsecret (source-generated P/Invoke) and Keychain stores behind `ISecretStore` ([AOT driver report](poc/aot-drivers.md) covers drivers only; see tasks P2-01 to P2-03 in the [task index](tasks/README.md)) |
| R7 | SSH tunnel under AOT | Medium | SSH.NET compatible (publish and startup only) ([AOT driver report](poc/aot-drivers.md)) |
| R8 | Mockups (15 screens M01-M15) in WinUI 3 and Avalonia | Medium | Workspace shell, connections, results, history, snippets and settings implemented on both heads (tasks P5-04 to P5-11 in the [task index](tasks/README.md)) |

## Order of work

Done through phase 6; the card list and statuses are in [docs/tasks/README.md](tasks/README.md).
