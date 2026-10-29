# SQuilL - Architecture note and risks (v0)

## Starting point (2026-10-29)

- SDK: .NET 11.0.100-rc.3 (target chosen by the project owner, see [ADR 0001](adr/0001-target-framework.md)). Design system and mockups: found (shared canvas), tokens extracted to `tokens/squill.tokens.json`.

## Verified stable versions (NuGet)

| Package | Version | Notes |
|---|---|---|
| Avalonia | 12.1.4 | AOT to validate |
| AvaloniaEdit | 0.10.12 (id `AvaloniaEdit`) | Old: check the fork/id compatible with Avalonia 12 |
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

## Risks and proofs of concept

| # | Risk | Severity | PoC to produce (exit criterion) |
|---|---|---|---|
| R1 | SQL editor: LoomEditor (WinUI 3, phase 11 of its roadmap, owner-maintained; Avalonia backend planned as its phase 12) with AvaloniaEdit as interim on Avalonia, see [ADR 0002](adr/0002-editor-and-terminal-components.md) | High | `ISqlEditor` abstraction + stub; `Squill.Sql` as in-process language-feature providers with LSP-shaped contracts; integrate LoomEditor (TextMate and providers API already exist). Criterion: highlighting + completion demo on 100,000 lines |
| R2 | WinUI 3 + Native AOT (XAML, MSIX, WinRT) | High | WinUI 3 shell published with AOT win-x64, zero IL2xxx/IL3xxx |
| R3 | Drivers under AOT: SqlClient (reflection, resources), Npgsql, MySqlConnector | High | One AOT console per driver: connect + query + read. Report: compatible / with caveats / incompatible |
| R4 | 1M-row grid | High | Custom-drawn control (no per-cell control), row+column virtualization, streaming source. Budget: 60 fps scrolling |
| R5 | Error-tolerant, multi-dialect SQL parser | Medium | ANTLR vs hand-written comparison. Initial leaning: hand-written (error recovery, typing latency, grammar size), one tokenizer per dialect |
| R6 | Cross-platform secret storage (libsecret under AOT, P/Invoke) | Medium | Source-generated P/Invoke (`LibraryImport`) to libsecret; Keychain via Security.framework; DPAPI |
| R7 | SSH tunnel under AOT | Medium | SSH.NET under AOT, otherwise an alternative |
| R8 | Mockups (15 screens M01-M15) to implement in WinUI 3 and Avalonia | Medium | Shell + Workspace screen (M03) on both heads |

## Proposed order of work

1. Solution + `Directory.Build.props` (AOT analyzers, warnings as errors, target per ADR 0001). Done.
2. `Squill.Core`, `Squill.Data` (`IDatabaseProvider`), SQLite provider, AOT PoC per driver (R3).
3. R4 and R1 PoCs on Avalonia first, then WinUI 3 (R2).
4. UI heads built from the mockups and the generated tokens.
