# Task cards

One card per file, `docs/tasks/phase-<n>/<id>-<slug>.md` (for example `phase-1/P1-01-solution-skeleton.md`), implemented one at a time by the `next-task` skill (`.claude/skills/next-task/`).

## Card format

```
# P1-01 - <title>

Status: todo | in progress | done
Depends on: <card ids, ranges allowed>

## Goal
## Design decisions (final)
## Out of scope
## Tasks (checkboxes)
## Acceptance criteria (extra commands, AOT publish, tests)
## Suggested commit message
```

## Conventions (apply to every card)

- Everything in the repository is English. Shell commands go through `rtk` (`rtk bypass <cmd>` only when the output is unusable).
- Verification: `rtk dotnet build Squill.slnx -c Release -warnaserror` (0 warnings) and `rtk bypass dotnet test --solution Squill.slnx -c Release`. Cards touching the WinUI head use `Squill.Windows.slnx` (created by P5-08) instead. Native AOT cards add `rtk dotnet publish <project> -c Release -r win-x64` with zero IL2xxx/IL3xxx warnings. Never weaken a test or add `NoWarn`/`#pragma warning disable` to get green.
- One card = one commit, signed, followed by a push (see `.claude/skills/next-task/SKILL.md`).
- Dependency graph (no cycles): `Squill.Core` <- `Squill.Data` <- `Squill.Data.<Driver>`; `Squill.Core` <- `Squill.Sql`; `Squill.Core` <- `Squill.Platform`; `Squill.Presentation` -> Core, Data, Sql, Platform (never a UI framework or a driver package); `Squill.App.*` heads -> Presentation + providers. `Squill.DesignSystem` and `tools/*` stay independent.
- New projects: minimal csproj (target framework, nullable, implicit usings, warnings-as-errors and AOT analyzers come from `Directory.Build.props`; only `OutputType`, `PackageReference` without version after P1-01, `ProjectReference`). Add them to `Squill.slnx` under `/src/`, `/tests/` or `/tools/`.
- Tests: one project per source project, `tests/<Project>.Tests`, copied from `tests/Squill.DesignSystem.Tests/Squill.DesignSystem.Tests.csproj` (xunit.v3, Microsoft.Testing.Platform). Test names `Method_Scenario_Expected`. Tests needing Docker run only when `SQUILL_DOCKER_TESTS=1` (xunit.v3 `[Fact(SkipUnless = ..., SkipType = ...)]`); others must run anywhere.
- Code: file-scoped namespaces, `sealed` by default, XML doc (`<summary>`, plus `<param>`/`<returns>` where they add information) on every public and protected type and member (CS1591 is an error), also on every member declared `public` inside an `internal` type, plus a one-line `<summary>` on every `internal` type (the compiler does not enforce this; check with a grep before each commit), C# 15 idioms, records for data, no reflection (`Activator`, `Type.GetType`, `dynamic`), JSON only through source-generated `JsonSerializerContext`s, SQL NULL is CLR `null`, async methods take a `CancellationToken`.
- NuGet versions come from `docs/architecture.md`; if one does not restore, use the closest version and record it there. Never add a package a card does not name.
- A card that cannot be done as written (API missing, AOT blocker, absent external checkout) stops with a report and a proposed card fix instead of improvising.

## Index

| Card | Title | Depends on | Status |
|---|---|---|---|
| [P1-01](phase-1/P1-01-central-package-management.md) | Central package management and editorconfig | none | done |
| [P1-02](phase-1/P1-02-ci-workflow.md) | GitHub Actions CI workflow | P1-01 | done |
| [P1-03](phase-1/P1-03-core-connection-profile.md) | Core: dialects and connection profile model | P1-01 | done |
| [P1-04](phase-1/P1-04-core-result-and-schema-model.md) | Core: column, schema and diagnostic models | P1-03 | done |
| [P1-05](phase-1/P1-05-data-contracts.md) | Data: provider contracts and registry | P1-04 | done |
| [P1-06](phase-1/P1-06-sqlite-provider.md) | Data.Sqlite: provider (connect, execute, stream) | P1-05 | done |
| [P1-07](phase-1/P1-07-sqlite-schema.md) | Data.Sqlite: schema reading | P1-06 | done |
| [P1-08](phase-1/P1-08-aot-probe-sqlite.md) | AOT probe for SQLite and the driver report | P1-07 | done |
| [P1-09](phase-1/P1-09-postgres-provider.md) | Data.Postgres: provider and AOT probe | P1-08 | done |
| [P1-10](phase-1/P1-10-postgres-schema.md) | Data.Postgres: schema reading | P1-09 | done |
| [P1-11](phase-1/P1-11-mysql-provider.md) | Data.MySql: provider and AOT probe | P1-10 | done |
| [P1-12](phase-1/P1-12-mysql-schema.md) | Data.MySql: schema reading | P1-11 | done |
| [P1-13](phase-1/P1-13-sqlserver-provider.md) | Data.SqlServer: provider and AOT probe | P1-12 | done |
| [P1-14](phase-1/P1-14-sqlserver-schema.md) | Data.SqlServer: schema reading | P1-13 | done |
| [P2-01](phase-2/P2-01-secret-store-dpapi.md) | Platform: secret store contract, in-memory and DPAPI stores | P1-03 | done |
| [P2-02](phase-2/P2-02-secret-store-libsecret.md) | Platform: libsecret secret store (Linux) | P2-01 | done |
| [P2-03](phase-2/P2-03-secret-store-keychain-factory.md) | Platform: macOS Keychain store and secret store factory | P2-02 | done |
| [P2-04](phase-2/P2-04-paths-and-profile-store.md) | Platform: app paths and JSON profile store | P2-03 | done |
| [P2-05](phase-2/P2-05-ssh-tunnel.md) | Platform: SSH tunnel abstraction and SSH.NET implementation | P2-04 | done |
| [P2-06](phase-2/P2-06-history-snippets-stores.md) | Platform: query history and snippet stores | P2-04 | done |
| [P2-07](phase-2/P2-07-settings-store.md) | Platform: application settings store | P2-06 | done |
| [P3-01](phase-3/P3-01-sql-tokenizer-core.md) | Sql: token model and generic tokenizer | P1-04 | done |
| [P3-02](phase-3/P3-02-tokenizers-postgres-sqlite.md) | Sql: PostgreSQL and SQLite tokenizers | P3-01 | done |
| [P3-03](phase-3/P3-03-tokenizers-mysql-tsql.md) | Sql: MySQL and T-SQL tokenizers | P3-02 | done |
| [P3-04](phase-3/P3-04-statement-splitter.md) | Sql: statement splitter and run-under-cursor | P3-03 | done |
| [P3-05](phase-3/P3-05-parser-decision-and-benchmark.md) | Sql: tokenizer benchmark and parser ADR | P3-04 | done |
| [P3-06](phase-3/P3-06-syntax-tree-and-expression-parser.md) | Sql: syntax tree and expression parser | P3-05 | done |
| [P3-07](phase-3/P3-07-select-parser.md) | Sql: SELECT statement parser | P3-06 | done |
| [P3-08](phase-3/P3-08-dml-ddl-parser.md) | Sql: INSERT, UPDATE, DELETE and CREATE TABLE parsers | P3-07 | done |
| [P3-09](phase-3/P3-09-scope-resolver.md) | Sql: scope analysis at a caret position | P3-08 | done |
| [P3-10](phase-3/P3-10-completion-contracts-keywords.md) | Sql: completion contracts and keyword completion | P3-09 | done |
| [P3-11](phase-3/P3-11-schema-completion.md) | Sql: table and column completion | P3-10 | done |
| [P3-12](phase-3/P3-12-diagnostics-and-hover.md) | Sql: diagnostics and hover | P3-11 | done |
| [P3-13](phase-3/P3-13-functions-and-signature-help.md) | Sql: function catalog, function completion and signature help | P3-12 | done |
| [P3-14](phase-3/P3-14-formatter.md) | Sql: formatter | P3-13, P2-07 | done |
| [P3-15](phase-3/P3-15-highlighter-and-grammars.md) | Sql: highlighter and TextMate grammars | P3-14 | done |
| [P4-01](phase-4/P4-01-mvvm-base-and-services.md) | Presentation: MVVM base and UI service contracts | P3-15, P2-07, P2-05, P1-14 | done |
| [P4-02](phase-4/P4-02-isqleditor-abstraction.md) | Presentation: ISqlEditor and run target resolution | P4-01 | done |
| [P4-03](phase-4/P4-03-connection-editor-vm.md) | Presentation: connection list and editor view-models | P4-01, P2-04 | done |
| [P4-04](phase-4/P4-04-session-service.md) | Presentation: connection sessions | P4-03, P2-05 | done |
| [P4-05](phase-4/P4-05-object-explorer-vm.md) | Presentation: object explorer view-model | P4-04 | done |
| [P4-06](phase-4/P4-06-grid-data-source.md) | Presentation: streaming grid data source and cell formatting | P4-04 | done |
| [P4-07](phase-4/P4-07-query-tab-vm.md) | Presentation: query tab and results view-models | P4-02, P4-06, P2-06 | done |
| [P4-08](phase-4/P4-08-query-parameters.md) | Presentation: query parameter prompt | P4-07 | done |
| [P4-09](phase-4/P4-09-workspace-vm-and-composition.md) | Presentation: workspace view-model and composition root | P4-08, P4-05, P4-03 | done |
| [P4-10](phase-4/P4-10-history-snippets-vm.md) | Presentation: history and snippets view-models | P4-09 | done |
| [P4-11](phase-4/P4-11-result-export.md) | Presentation: result export | P4-06 | done |
| [P4-12](phase-4/P4-12-settings-vm.md) | Presentation: settings view-model | P4-09 | done |
| [P5-01](phase-5/P5-01-avalonia-head-skeleton.md) | Avalonia head: skeleton, tokens and AOT publish | P4-12 | done |
| [P5-02](phase-5/P5-02-avalonia-grid-control.md) | Avalonia head: custom-drawn virtualized grid | P5-01 | done |
| [P5-03](phase-5/P5-03-avalonia-editor.md) | Avalonia head: SQL editor on AvaloniaEdit | P5-02 | done |
| [P5-04](phase-5/P5-04-avalonia-workspace-shell.md) | Avalonia head: workspace shell | P5-03 | done |
| [P5-05](phase-5/P5-05-avalonia-connection-ui.md) | Avalonia head: connection dialog and list | P5-04 | done |
| [P5-06](phase-5/P5-06-avalonia-results-panel.md) | Avalonia head: results panel, selection, copy and export | P5-05, P4-11 | done |
| [P5-07](phase-5/P5-07-avalonia-history-settings-ui.md) | Avalonia head: history, snippets and settings views | P5-06 | done |
| [P5-08](phase-5/P5-08-winui-head-skeleton.md) | WinUI head: skeleton and Native AOT publish | P5-07 | done |
| [P5-09](phase-5/P5-09-winui-plain-editor.md) | WinUI head: plain-text SQL editor | P5-08 | done |
| [P5-10](phase-5/P5-10-winui-grid-win2d.md) | WinUI head: custom-drawn grid on Win2D | P5-09 | done |
| [P5-11](phase-5/P5-11-winui-workspace-shell.md) | WinUI head: workspace shell and connections | P5-10 | done |
| [P5-12](phase-5/P5-12-winui-results-history-settings.md) | WinUI head: results panel, history, snippets and settings | P5-11 | done |
| [P5-13](phase-5/P5-13-loomeditor-integration.md) | WinUI head: LoomEditor integration | P5-12, P3-15 | done |
| [P6-01](phase-6/P6-01-terminal-tab-vm.md) | Presentation: terminal tab view-model | P5-12 | done |
| [P6-02](phase-6/P6-02-loomterminal-integration.md) | Heads: LoomTerminal tabs | P6-01 | done |
| [P6-03](phase-6/P6-03-velopack-packaging.md) | Windows packaging with Velopack and release workflow | P6-02, P1-02 | done |
| [P6-04](phase-6/P6-04-readme-and-user-docs.md) | Project README and build instructions | P6-03 | done |
| [P7-01](phase-7/P7-01-core-schema-model-extension.md) | Core: schema model extension (auto-increment, referential actions, key name) | P1-04 | done |
| [P7-02](phase-7/P7-02-sqlite-schema-extension.md) | Data.Sqlite: read auto-increment and foreign-key actions | P7-01, P1-07 | done |
| [P7-03](phase-7/P7-03-postgres-schema-extension.md) | Data.Postgres: read auto-increment, foreign-key actions and key name | P7-01, P1-10 | done |
| [P7-04](phase-7/P7-04-mysql-schema-extension.md) | Data.MySql: read auto-increment and foreign-key actions | P7-01, P1-12 | done |
| [P7-05](phase-7/P7-05-sqlserver-schema-extension.md) | Data.SqlServer: read identity, foreign-key actions and key name | P7-01, P1-14 | done |
| [P7-06](phase-7/P7-06-core-table-design-model.md) | Core: table design model | P7-01 | done |
| [P7-07](phase-7/P7-07-core-table-diff.md) | Core: table diff into schema operations | P7-06 | done |
| [P7-08](phase-7/P7-08-core-design-validation.md) | Core: table design validation | P7-07 | done |
| [P7-09](phase-7/P7-09-sql-quoting-and-type-catalog.md) | Sql: identifier quoting and data type catalog | P1-04 | done |
| [P7-10](phase-7/P7-10-sql-create-drop-table.md) | Sql: DDL generator contract, CREATE TABLE and DROP TABLE | P7-09, P7-06 | done |
| [P7-11](phase-7/P7-11-sql-alter-postgres.md) | Sql: ALTER TABLE for PostgreSQL | P7-10, P7-07 | done |
| [P7-12](phase-7/P7-12-sql-alter-mysql.md) | Sql: ALTER TABLE for MySQL | P7-10, P7-07 | done |
| [P7-13](phase-7/P7-13-sql-alter-sqlserver.md) | Sql: ALTER TABLE for SQL Server | P7-10, P7-07 | done |
| [P7-14](phase-7/P7-14-sql-alter-sqlite.md) | Sql: ALTER TABLE for SQLite with table rebuild | P7-10, P7-07 | done |
| [P7-15](phase-7/P7-15-presentation-er-model.md) | Presentation: ER model built from a schema snapshot | P7-01, P4-05 | done |
| [P7-16](phase-7/P7-16-presentation-er-layout.md) | Presentation: ER diagram node layout | P7-15 | done |
| [P7-17](phase-7/P7-17-presentation-er-edge-routing.md) | Presentation: ER diagram edge routing | P7-16 | done |
| [P7-18](phase-7/P7-18-platform-diagram-layout-store.md) | Platform: diagram layout store | P2-04 | done |
| [P7-19](phase-7/P7-19-presentation-er-diagram-vm.md) | Presentation: ER diagram view-model | P7-15 ... P7-18, P4-04 | done |
| [P7-20](phase-7/P7-20-presentation-er-svg-export.md) | Presentation: ER diagram SVG export | P7-19 | done |
| [P7-21](phase-7/P7-21-presentation-table-designer-vm.md) | Presentation: table designer view-model | P7-08, P7-10 ... P7-14, P4-04 | done |
| [P7-22](phase-7/P7-22-presentation-workspace-integration.md) | Presentation: workspace integration of designer and diagram tabs | P7-19 ... P7-21, P4-09, P6-01 | done |
| [P7-23](phase-7/P7-23-avalonia-er-diagram.md) | Avalonia head: ER diagram tab | P7-22, P7-20, P5-06 | done |
| [P7-24](phase-7/P7-24-avalonia-table-designer-view.md) | Avalonia head: table designer view | P7-23, P7-21 | todo |
| [P7-25](phase-7/P7-25-winui-er-diagram.md) | WinUI head: ER diagram tab | P7-22, P7-20, P5-12 | todo |
| [P7-26](phase-7/P7-26-winui-table-designer-view.md) | WinUI head: table designer view | P7-25, P7-21 | todo |
| [P7-27](phase-7/P7-27-docs-and-adr.md) | Documentation: schema designer ADR, README and architecture note | P7-24, P7-26, P6-04 | todo |
