# SQuilL - Architecture note and risks (v0)

## Starting point (2026-10-29)

- SDK: .NET 11.0.100 GA (target chosen by the project owner, see [ADR 0001](adr/0001-target-framework.md)). Design system and mockups: found (shared canvas), tokens extracted to `tokens/squill.tokens.json`.

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
| Microsoft.Data.Sqlite | 11.0.0 | Pinned in `Directory.Packages.props` (GA; was 11.0.0-rc.3.26522.148 until the GA migration) |
| Antlr4.Runtime.Standard | 4.13.1 | |
| SSH.NET | 2026.0.0 | AOT to validate |
| Testcontainers | 4.16.0 | |
| BenchmarkDotNet | 0.15.8 | |
| xunit.v3 | 4.0.2 | |
| Velopack | 1.2.161 | Installer and auto-update ([ADR 0009](adr/0009-release-and-auto-update.md)) |

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
| R9 | Installer and automatic updates (Velopack under Native AOT) | High | Met: `UpdateManager` publishes with 0 IL2xxx/IL3xxx warnings and installs, updates (delta) and uninstalls from a local feed without touching `%APPDATA%\SQuilL`; closed by phase 12: the end-to-end run (pack, silent install, update through a local feed, uninstall) passes with the data directory byte-identical ([verification](verification/release.md)); arm64 is built on CI only and untested locally ([Velopack report](poc/velopack-aot.md), [ADR 0009](adr/0009-release-and-auto-update.md)) |

## Order of work

Done through phase 13 (execution plan, see [ADR 0010](adr/0010-execution-plan.md); the manual checklist of [verification/plan.md](verification/plan.md) is still pending), phase 12 (installer and automatic updates, see [ADR 0009](adr/0009-release-and-auto-update.md); the manual checklist of [verification/release.md](verification/release.md) is still pending); phase 11 (engine parity, see [ADR 0008](adr/0008-engine-parity.md); the manual window checks of P11-36 to P11-39 are still pending); phase 10 (localization, see [ADR 0007](adr/0007-localization.md)); phase 9: (visual conformance, see [ADR 0006](adr/0006-visual-conformance.md)); phase 8 and 7 added (schema designer and ER diagram, see [ADR 0004](adr/0004-schema-designer.md); editable data grid, see [ADR 0005](adr/0005-grid-editing.md)); the card list and statuses are in [docs/tasks/README.md](tasks/README.md).

## Engine parity (phase 11)

- **Real servers.** PostgreSQL, MySQL and SQL Server tests run against Testcontainers (`eng/run-docker-tests.ps1` with Podman, real Docker in CI), see [testing](testing.md). Each engine has a report in [docs/verification/](verification/sqlite.md) whose manual checklist is still to be done by the owner.
- **Shared contracts grew.** `IResultReader` (`NextResultAsync`, `Messages`), `IDatabaseConnection.ListDatabasesAsync`, `SchemaSnapshot` (routines, triggers, definitions) and the connection options of `ConnectionProfile`. The support matrix and the unsupported list are in [ADR 0008](adr/0008-engine-parity.md).

## UI text, fonts and profile fields (phases 9 and 10)

- **String catalog.** `UiText` in `Squill.Presentation` serves French or English strings (`UiLanguage`, plurals through `FormatPlural`) from `FrozenDictionary`s built from `UiTextCatalogFr` / `UiTextCatalogEn`; heads reference keys through the `{loc:Text}` markup extension (Avalonia) and `Loc.Key` (WinUI). A test checks that every key used in `src/` exists.
- **Fonts.** JetBrains Mono, Inter and Inter Tight are bundled (OFL) and registered as `SquillFont*` resources in both heads; the type scale, spacing and shadow tokens are generated by `Squill.DesignSystem`.
- **Environment and folder.** `ConnectionProfile` has `Environment` and `Folder` (defaulted, so older profile files load unchanged); they drive the environment pills, tab stripes, status bar block and the folder grouping of the explorer tree.

## Installer and updates (phase 12)

- **Layers.** `Squill.Platform.Updates` holds the update engine (`IUpdateEngine`, the only code that references `Velopack`), the schedule and the state store; `Squill.Presentation.Updates` holds the shared view-model, release notes and banner layout used by both heads. `Squill.Platform.Lifecycle` tracks the last run version (`InstallLifecycle`). Only the WinUI head wires `VelopackApp` and its hooks (`Program.cs`).
- **Version.** One source, `eng/Version.props`; the release tag is `v` + that value, the runtime reads it through the generated `AppVersion`.
- **Data.** The install root is `%LOCALAPPDATA%\SQuilL`; user data stays in `%APPDATA%\SQuilL` and survives updates and uninstall. The maintainer and user guide is [release.md](release.md).

## SQL completion (phase 14)

- **Where.** `Squill.Sql/Completion`: `ISqlCompletionProvider`, `KeywordCompletionProvider`, `SchemaCompletionProvider`, `FunctionCompletionProvider`, `SnippetCompletionProvider`, merged by `CompositeCompletionProvider` ordered by `SortText` (`CompletionOrder`); join logic in `JoinContext`, `TableRelationFinder`, `AliasGenerator`, `JoinConditionText`, `JoinRows`, `OnClauseRows`; text helpers `TableReference`, `IdentifierQuoting`, `KeywordCasing`, `SnippetText`, `CompletionDocumentation`, `CompletionTrigger`.
- **Wiring.** `QueryTabViewModel.SessionCompletionProvider` adds `session.Schema` and the keyword-case setting to each request; `src/Squill.App.Shared/LoomSqlProviders.cs` (linked into both heads) slices the script to the statement under the caret and maps `CompletionItem` to LoomEditor's; `PlainSqlEditor` is the fallback. Decisions, alternatives and known limits: [ADR 0011](adr/0011-sql-completion.md); evidence and manual checklist: [verification/completion.md](verification/completion.md).

## Execution plan (phase 13)

- **Layers.** `Squill.Core/Plans` holds the model (`QueryPlan`, `PlanNode`, `PlanWarning`, `PlanSuggestion`) and `PlanAnalysis` (relative cost, critical nodes, misestimates). `Squill.Data` has the optional `IPlanReader` (the four connections implement it; `ConnectionSession.PlanSupport` exposes it); each driver has an internal `<Driver>PlanReader` and a pure `<Driver>PlanParser`. `Squill.Sql/Plans` has `PlanEligibility` (what may be explained or analyzed). `Squill.Presentation/Plans` has `PlanViewModel`, `PlanTabViewModel` and `PlanLayout`; `QueryTabViewModel` has `ExplainCommand` / `AnalyzeCommand` and `WorkspaceViewModel.Plans` holds the plan tabs. Both heads draw `PlanTabView`, the table, the details panel and the graph canvas.
- **The plan tab** is a workspace tab of its own (one per query tab, closed with it); a plan writes no history entry. Decisions, per-engine mechanisms and known limits: [ADR 0010](adr/0010-execution-plan.md).
