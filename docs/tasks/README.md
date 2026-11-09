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
- Never type a version outside `eng/Version.props` (read it through `AppVersion.Current` or `eng/get-version.ps1`).
- Dependency graph (no cycles): `Squill.Core` <- `Squill.Data` <- `Squill.Data.<Driver>`; `Squill.Core` <- `Squill.Sql`; `Squill.Core` <- `Squill.Platform`; `Squill.Presentation` -> Core, Data, Sql, Platform (never a UI framework or a driver package); `Squill.App.*` heads -> Presentation + providers. `Squill.DesignSystem` and `tools/*` stay independent.
- New projects: minimal csproj (target framework, nullable, implicit usings, warnings-as-errors and AOT analyzers come from `Directory.Build.props`; only `OutputType`, `PackageReference` without version after P1-01, `ProjectReference`). Add them to `Squill.slnx` under `/src/`, `/tests/` or `/tools/`.
- Tests: one project per source project, `tests/<Project>.Tests`, copied from `tests/Squill.DesignSystem.Tests/Squill.DesignSystem.Tests.csproj` (xunit.v3, Microsoft.Testing.Platform). Test names `Method_Scenario_Expected`. Tests needing Docker run only when `SQUILL_DOCKER_TESTS=1` (xunit.v3 `[Fact(SkipUnless = ..., SkipType = ...)]`); others must run anywhere.
- Code: file-scoped namespaces, `sealed` by default, XML doc (`<summary>`, plus `<param>`/`<returns>` where they add information) on every public and protected type and member (CS1591 is an error), also on every member declared `public` inside an `internal` type, plus a one-line `<summary>` on every `internal` type (the compiler does not enforce this; check with a grep before each commit), C# 15 idioms, records for data, no reflection (`Activator`, `Type.GetType`, `dynamic`), JSON only through source-generated `JsonSerializerContext`s, SQL NULL is CLR `null`, async methods take a `CancellationToken`.
- NuGet versions come from `docs/architecture.md`; if one does not restore, use the closest version and record it there. Never add a package a card does not name.
- User-visible text goes through `UiText`; see phase 10.
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
| [P7-24](phase-7/P7-24-avalonia-table-designer-view.md) | Avalonia head: table designer view | P7-23, P7-21 | done |
| [P7-25](phase-7/P7-25-winui-er-diagram.md) | WinUI head: ER diagram tab | P7-22, P7-20, P5-12 | done |
| [P7-26](phase-7/P7-26-winui-table-designer-view.md) | WinUI head: table designer view | P7-25, P7-21 | done |
| [P7-27](phase-7/P7-27-docs-and-adr.md) | Documentation: schema designer ADR, README and architecture note | P7-24, P7-26, P6-04 | done |
| [P8-01](phase-8/P8-01-core-edit-model.md) | Core: row edit model | P7-01 | done |
| [P8-02](phase-8/P8-02-sql-editable-source-analyzer.md) | Sql: editable source analysis | P8-01, P3-07, P1-04 | done |
| [P8-03](phase-8/P8-03-sql-dml-generator.md) | Sql: INSERT, UPDATE and DELETE generation | P8-01, P7-09 | done |
| [P8-04](phase-8/P8-04-sql-generated-key-readback.md) | Sql: reading generated keys after an INSERT | P8-03 | done |
| [P8-05](phase-8/P8-05-presentation-cell-value-parsing.md) | Presentation: parsing edited cell text into typed values | P4-06 | done |
| [P8-06](phase-8/P8-06-presentation-editable-grid-data-source.md) | Presentation: editable grid data source with pending changes | P8-01, P4-06 | done |
| [P8-07](phase-8/P8-07-presentation-change-set-builder.md) | Presentation: change set and validation | P8-06, P8-01 | done |
| [P8-08](phase-8/P8-08-presentation-statement-runner.md) | Presentation: shared statement runner (refactor of the designer apply flow) | P7-21, P4-04 | done |
| [P8-09](phase-8/P8-09-presentation-edit-committer.md) | Presentation: committing a change set | P8-03, P8-04, P8-07, P8-08 | done |
| [P8-10](phase-8/P8-10-presentation-result-editor-vm.md) | Presentation: result editor view-model | P8-05, P8-06, P8-07, P8-09, P4-01 | done |
| [P8-11](phase-8/P8-11-presentation-grid-edit-controller.md) | Presentation: grid edit controller (shared keyboard and cell-edit logic) | P8-10, P5-06 | done |
| [P8-12](phase-8/P8-12-presentation-query-tab-integration.md) | Presentation: editable results in query tabs | P8-02, P8-10, P8-11, P4-09, P7-22 | done |
| [P8-13](phase-8/P8-13-data-dml-round-trip-tests.md) | Data providers: DML round-trip tests on every dialect | P8-04, P8-08, P1-14 | done |
| [P8-14](phase-8/P8-14-avalonia-grid-editing.md) | Avalonia head: in-place cell editing in the grid | P8-12, P5-06 | done |
| [P8-15](phase-8/P8-15-avalonia-results-edit-toolbar.md) | Avalonia head: edit toolbar, issues and commit | P8-14 | done |
| [P8-16](phase-8/P8-16-winui-grid-editing.md) | WinUI head: in-place cell editing in the grid | P8-12, P5-12 | done |
| [P8-17](phase-8/P8-17-winui-results-edit-toolbar.md) | WinUI head: edit toolbar, issues and commit | P8-16, P8-15 | done |
| [P8-18](phase-8/P8-18-docs-and-adr.md) | Documentation: grid editing ADR, README and architecture note | P8-15, P8-17, P8-13, P7-27 | done |
| [P9-01](phase-9/P9-01-tokens-and-fonts.md) | Design system: font, type scale and missing tokens, bundled fonts | none | done |
| [P9-02](phase-9/P9-02-visual-tooling.md) | Tools: sample data and window capture for visual comparison | none | done |
| [P9-03](phase-9/P9-03-localization-catalog.md) | Presentation: French string catalog | none | done |
| [P9-04](phase-9/P9-04-profile-environment-folder.md) | Core and Platform: connection environment and folder | none | done |
| [P9-05](phase-9/P9-05-avalonia-control-styles.md) | Avalonia head: shared control styles | P9-01, P9-03, P9-02 | done |
| [P9-06](phase-9/P9-06-winui-control-styles.md) | WinUI head: shared control styles | P9-01, P9-03, P9-02 | done |
| [P9-07](phase-9/P9-07-avalonia-window-chrome.md) | Avalonia head: title bar, menu, activity rail and status bar | P9-04, P9-05, P9-02 | done |
| [P9-08](phase-9/P9-08-avalonia-explorer-and-tabs.md) | Avalonia head: object explorer, tab strip and properties panel | P9-07, P9-02 | done |
| [P9-09](phase-9/P9-09-winui-window-chrome.md) | WinUI head: title bar, menu, activity rail and status bar | P9-04, P9-06, P9-02 | done |
| [P9-10](phase-9/P9-10-winui-explorer-and-tabs.md) | WinUI head: object explorer, tab strip and properties panel | P9-09, P9-02 | done |
| [P9-11](phase-9/P9-11-avalonia-editor-toolbar-panels.md) | Avalonia head: query toolbar, editor frame and history panel | P9-08, P9-02 | done |
| [P9-12](phase-9/P9-12-winui-editor-toolbar-panels.md) | WinUI head: query toolbar, editor frame and history panel | P9-10, P9-02 | done |
| [P9-13](phase-9/P9-13-avalonia-results-panel.md) | Avalonia head: results panel chrome, pending-edit banner and export menu | P9-11, P9-02 | done |
| [P9-14](phase-9/P9-14-avalonia-grid-visuals.md) | Avalonia head: grid canvas drawing | P9-13, P9-02 | done |
| [P9-15](phase-9/P9-15-winui-results-panel.md) | WinUI head: results panel chrome, pending-edit banner and export menu | P9-12, P8-17, P9-02 | done |
| [P9-16](phase-9/P9-16-winui-grid-visuals.md) | WinUI head: Win2D grid drawing | P9-15, P8-16, P9-02 | done |
| [P9-17](phase-9/P9-17-avalonia-connection-manager.md) | Avalonia head: connection manager window | P9-04, P9-05, P9-02 | done |
| [P9-18](phase-9/P9-18-winui-connection-manager.md) | WinUI head: connection manager dialog | P9-04, P9-06, P9-02 | done |
| [P9-19](phase-9/P9-19-avalonia-table-designer.md) | Avalonia head: table designer view | P9-08, P9-02 | done |
| [P9-20](phase-9/P9-20-winui-table-designer.md) | WinUI head: table designer view | P9-10, P9-02 | done |
| [P9-21](phase-9/P9-21-avalonia-er-diagram.md) | Avalonia head: ER diagram view | P9-08, P9-02 | done |
| [P9-22](phase-9/P9-22-winui-er-diagram.md) | WinUI head: ER diagram view | P9-10, P9-02 | done |
| [P9-23](phase-9/P9-23-avalonia-settings.md) | Avalonia head: settings window | P9-05, P9-03, P9-02 | done |
| [P9-24](phase-9/P9-24-winui-settings.md) | WinUI head: settings window | P9-06, P9-03, P9-02 | done |
| [P9-25](phase-9/P9-25-docs-and-adr.md) | Documentation: visual conformance ADR, README and architecture note | P9-07 ... P9-24 | done |
| [P9-26](phase-9/P9-26-unified-explorer-tree.md) | Explorer: one tree connection > database > schema > objects | P9-08, P9-10 | done |
| [P9-27](phase-9/P9-27-explorer-folder-counts.md) | Explorer: object counts on schema and folder rows (both heads) | P9-26 | done |
| [P10-01](phase-10/P10-01-platform-language-setting.md) | Platform: language setting | none | done |
| [P10-02](phase-10/P10-02-bilingual-catalog.md) | Presentation: bilingual string catalog (fr and en, plurals, parity tests) | P10-01 | done |
| [P10-03](phase-10/P10-03-string-inventory.md) | Tests: hard-coded string scanner and inventory | none | done |
| [P10-04](phase-10/P10-04-startup-language-and-settings-vm.md) | Presentation: language at startup and in the settings view-model | P10-01, P10-02 | done |
| [P10-05](phase-10/P10-05-avalonia-settings-language.md) | Avalonia head: language choice in the settings window | P10-04 | done |
| [P10-06](phase-10/P10-06-winui-settings-language.md) | WinUI head: language choice in the settings window | P10-04 | done |
| [P10-07](phase-10/P10-07-formatting-culture.md) | Heads and Presentation: number and date formatting follows the language | P10-02 | done |
| [P10-08](phase-10/P10-08-winui-loc-properties.md) | WinUI head: catalog markup for tooltips, headers, titles and menus | P10-02 | done |
| [P10-09](phase-10/P10-09-message-codes.md) | Core and Sql: stable message codes for issues and diagnostics | P10-02 | done |
| [P10-10](phase-10/P10-10-migrate-connections.md) | Migrate strings: connection editor, validation and list | P10-03, P10-04, P10-07, P10-08 | done |
| [P10-11](phase-10/P10-11-migrate-explorer-and-chrome.md) | Migrate strings: explorer, menus, window chrome and tabs | P10-03, P10-04, P10-07, P10-08 | done |
| [P10-12](phase-10/P10-12-migrate-query-and-status.md) | Migrate strings: query tab, parameters, status bar, diagnostics | P10-03, P10-04, P10-07, P10-08, P10-09 | done |
| [P10-13](phase-10/P10-13-migrate-results-and-grid.md) | Migrate strings: results, grid editing and export | P10-03, P10-04, P10-07, P10-08, P10-09 | done |
| [P10-14](phase-10/P10-14-migrate-designer-and-er.md) | Migrate strings: table designer and ER diagram | P10-03, P10-04, P10-07, P10-08, P10-09 | done |
| [P10-15](phase-10/P10-15-migrate-history-snippets-terminal.md) | Migrate strings: history, snippets and terminal | P10-03, P10-04, P10-07, P10-08 | done |
| [P10-16](phase-10/P10-16-migrate-settings-dialogs-errors.md) | Migrate strings: settings, dialogs, errors and leftovers | P10-10 ... P10-15, P10-05, P10-06 | done |
| [P10-17](phase-10/P10-17-enforcement-tests.md) | Tests: completeness guards for localization | P10-16 | done |
| [P10-18](phase-10/P10-18-docs-and-adr.md) | Documentation: localization ADR 0007, README and architecture note | P10-17 | done |
| [P11-01](phase-11/P11-01-podman-test-runner.md) | Podman runner for the Docker-gated tests | none | done |
| [P11-02](phase-11/P11-02-postgres-container-tests.md) | PostgreSQL container tests run and pass | P11-01 | done |
| [P11-03](phase-11/P11-03-mysql-container-tests.md) | MySQL container tests run and pass | P11-01 | done |
| [P11-04](phase-11/P11-04-sqlserver-container-tests.md) | SQL Server container tests run and pass | P11-01 | done |
| [P11-05](phase-11/P11-05-ci-docker-tests.md) | CI runs the Docker-gated tests on Linux | P11-02 ... P11-04 | done |
| [P11-06](phase-11/P11-06-core-connection-options.md) | Core: connection options (TLS, timeouts) | none | done |
| [P11-07](phase-11/P11-07-data-connection-options.md) | Data: apply the connection options in the four providers | P11-06, P11-04 | done |
| [P11-08](phase-11/P11-08-winui-connection-options.md) | WinUI head: connection options in the connection editor | P11-06, P11-07 | done |
| [P11-09](phase-11/P11-09-avalonia-connection-options.md) | Avalonia head: connection options in the connection manager |X|
| [P11-10](phase-11/P11-10-cancel-and-timeout.md) | Cancel and timeout proven on every engine | P11-07 | done |
| [P11-11](phase-11/P11-11-data-multiple-result-sets.md) | Data: multiple result sets and server messages | P11-04 | done |
| [P11-12](phase-11/P11-12-presentation-multiple-results.md) | Presentation: several results and messages per statement | P11-11 | done |
| [P11-13](phase-11/P11-13-winui-multiple-results.md) | WinUI head: result tabs for several sets and a Messages tab | P11-12 | done |
| [P11-14](phase-11/P11-14-avalonia-multiple-results.md) | Avalonia head: result tabs for several sets and a Messages tab | P11-12 | done |
| [P11-15](phase-11/P11-15-core-schema-objects-model.md) | Core: routines, triggers, view definitions and materialized views | none | done |
| [P11-16](phase-11/P11-16-sqlite-schema-objects.md) | Data.Sqlite: triggers and view definitions, SQLite sample schema | P11-15 | done |
| [P11-17](phase-11/P11-17-postgres-schema-objects.md) | Data.Postgres: routines, triggers, view definitions, materialized views | P11-15, P11-02 | done |
| [P11-18](phase-11/P11-18-mysql-schema-objects.md) | Data.MySql: routines, triggers, view definitions | P11-15, P11-03 | done |
| [P11-19](phase-11/P11-19-sqlserver-schema-objects.md) | Data.SqlServer: routines, triggers, view definitions | P11-15, P11-04 | done |
| [P11-20](phase-11/P11-20-presentation-explorer-objects.md) | Presentation: routines, triggers and open definition in the explorer | P11-15 | done |
| [P11-21](phase-11/P11-21-winui-explorer-objects.md) | WinUI head: explorer glyphs and menu for routines, triggers, definitions | P11-20 | done |
| [P11-22](phase-11/P11-22-avalonia-explorer-objects.md) | Avalonia head: explorer glyphs and menu for routines, triggers, definitions | P11-20 | done |
| [P11-23](phase-11/P11-23-data-list-databases.md) | Data: list the databases of a server | P11-02 ... P11-04 | done |
| [P11-24](phase-11/P11-24-presentation-database-switch.md) | Presentation: browse other databases of a connection | P11-23 | done |
| [P11-25](phase-11/P11-25-column-types-read.md) | Column types read and displayed correctly on every engine | P11-02 ... P11-04, P11-16 | done |
| [P11-26](phase-11/P11-26-column-types-edit.md) | Column types edited and written back correctly on every engine | P11-25 | done |
| [P11-27](phase-11/P11-27-sql-corpus-parser-diagnostics.md) | Dialect corpus: parser and diagnostics against real servers | P11-02 ... P11-04 | done |
| [P11-28](phase-11/P11-28-sql-completion-hover-format-dialects.md) | Completion, hover, signature help and formatter per dialect | P11-27, P11-17 ... P11-19 | done |
| [P11-29](phase-11/P11-29-designer-ddl-sqlite.md) | Table designer on SQLite: create and alter matrix | P11-16 | done |
| [P11-30](phase-11/P11-30-designer-ddl-servers.md) | Table designer DDL on PostgreSQL, MySQL and SQL Server | P11-02 ... P11-04 | done |
| [P11-31](phase-11/P11-31-er-diagram-engines.md) | ER diagram on real schemas of every engine | P11-02 ... P11-04, P11-16 | done |
| [P11-32](phase-11/P11-32-export-engines.md) | Result export verified per engine | P11-25 | done |
| [P11-33](phase-11/P11-33-grid-edit-commit-engines.md) | Grid edit and commit through the Presentation layer on every engine | P11-02 ... P11-04, P11-16, P11-26 | done |
| [P11-34](phase-11/P11-34-terminal-engine-clients.md) | Terminal: native client launch per engine | P11-06 | done |
| [P11-35](phase-11/P11-35-ssh-tunnel-containers.md) | SSH tunnel proven against real servers | P11-02 ... P11-04 | done |
| [P11-36](phase-11/P11-36-verify-sqlite.md) | End-to-end verification: SQLite (WinUI first, Avalonia smoke) | P11-10, P11-13, P11-21, P11-25, P11-29, P11-33 | automated done, manual pending |
| [P11-37](phase-11/P11-37-verify-postgres.md) | End-to-end verification: PostgreSQL | P11-36, P11-17, P11-24, P11-27 ... P11-32, P11-35 | automated done, manual pending |
| [P11-38](phase-11/P11-38-verify-mysql.md) | End-to-end verification: MySQL | P11-37, P11-18, P11-24, P11-27 ... P11-32 | automated done, manual pending |
| [P11-39](phase-11/P11-39-verify-sqlserver.md) | End-to-end verification: SQL Server | P11-37, P11-19, P11-24, P11-27 ... P11-32 | automated done, manual pending |
| [P11-40](phase-11/P11-40-docs-and-adr.md) | Documentation: engine parity ADR 0008, README and architecture note | P11-36 ... P11-39 | done |
| [P12-01](phase-12/P12-01-velopack-aot-poc.md) | Velopack under Native AOT: proof of concept and report | none | done |
| [P12-02](phase-12/P12-02-version-source-of-truth.md) | Version: single source of truth, assembly version and AppVersion | none | done |
| [P12-03](phase-12/P12-03-core-semantic-version.md) | Core: semantic version parsing and comparison | P12-02 | done |
| [P12-04](phase-12/P12-04-changelog-and-release-notes.md) | Changelog and release notes extraction | P12-02 | done |
| [P12-05](phase-12/P12-05-pack-script.md) | Pack script: AOT publish and `vpk pack` for a channel | P12-01, P12-02, P12-04 | done |
| [P12-06](phase-12/P12-06-install-lifecycle-and-data-dir.md) | Install lifecycle: first run, update hooks, uninstall, data directory kept | P12-01, P12-02 | done |
| [P12-07](phase-12/P12-07-platform-update-settings.md) | Platform: update settings and persisted update state | P12-03 | todo |
| [P12-08](phase-12/P12-08-platform-update-service.md) | Platform: update service (state machine over an engine contract) | P12-03, P12-07 | todo |
| [P12-09](phase-12/P12-09-velopack-update-engine.md) | Platform: Velopack update engine (GitHub Releases or local feed) | P12-01, P12-08 | todo |
| [P12-10](phase-12/P12-10-presentation-release-notes.md) | Presentation: release notes model (markdown subset) | P12-04 | todo |
| [P12-11](phase-12/P12-11-presentation-update-view-model.md) | Presentation: update view-model and composition | P12-06, P12-08, P12-09, P12-10 | todo |
| [P12-12](phase-12/P12-12-presentation-settings-about.md) | Presentation: update settings and About view-models | P12-11 | todo |
| [P12-13](phase-12/P12-13-winui-settings-about.md) | WinUI head: update rows in the settings window and the About dialog | P12-12, P9-24 | todo |
| [P12-14](phase-12/P12-14-winui-update-banner-and-dialog.md) | WinUI head: update notice, release notes and restart to update | P12-13 | todo |
| [P12-15](phase-12/P12-15-avalonia-update-ui.md) | Avalonia head: update settings, About, notice and dialog | P12-14, P9-23 | todo |
| [P12-16](phase-12/P12-16-release-workflow.md) | Release workflow: tag to GitHub Release | P12-05, P12-01 | todo |
| [P12-17](phase-12/P12-17-optional-code-signing.md) | Optional code signing in the pack script and the release workflow | P12-16 | todo |
| [P12-18](phase-12/P12-18-windows-arm64.md) | Windows arm64 build and package (conditional) | P12-16, P12-01 | todo |
| [P12-19](phase-12/P12-19-verify-install-and-update.md) | End-to-end verification: install, update from N to N+1, uninstall | P12-05, P12-06, P12-09, P12-14, P12-15, P12-16 | todo |
| [P12-20](phase-12/P12-20-docs-and-adr.md) | Documentation: release guide, ADR 0009, README and architecture note | P12-17 ... P12-19 | todo |
| [P12-21](phase-12/P12-21-avalonia-linux-macos-packaging.md) | Avalonia head: Linux and macOS packaging with Velopack (optional) | P12-15, P12-16, P12-20 | todo |

### Phase 9 - Visual conformance

Goal: make both heads look like the owner's high-fidelity mockups (`docs/design/mockups/`, numbers and decisions in `docs/design/visual-spec.md`). Phase 9 changes appearance and layout, not features: mockup elements without a feature behind them are listed in `visual-spec.md` section 10 and are not drawn.

- Branch: implement phase 9 on a new branch `phase-9-visual-conformance`, cut from `main` **after phase 8 is merged** (P9-15 and P9-16 edit files of P8-16/P8-17). Do not start it from `phase-8-data-grid-editing`.
- Order: P9-01 ... P9-04 are foundations and tools (any order, P9-01 and P9-02 first), then styles (P9-05/06), then each head screen by screen. Avalonia and WinUI cards of the same screen are independent of each other.
- Cards that need a screenshot use the tools of P9-02 and the procedure of `visual-spec.md` section 11; a card whose screen cannot be driven must say what the owner has to check by hand.

### Phase 10 - Localization fr/en

Goal: the whole application is bilingual French / English in both heads. Decisions (final, details in the cards and in ADR 0007 written by P10-18): two catalogs with identical keys (`UiTextCatalogFr`, `UiTextCatalogEn`), plurals through `FormatPlural`, setting `System default / Francais / English` (System = French when the OS UI language is French, otherwise English), the language applies after a restart, numbers and dates follow the language, database / driver messages, SQL and user data are never translated, completeness is enforced by tests (catalog parity, used keys, scanner over code and XAML).

- Branch: implement phase 10 on the existing branch `phase-10-localization` (cut from the unmerged `fix-winui-connection-file-picker`, commit `e85c84b`); do not create another branch.
- Order: P10-01 ... P10-04 are the foundation (P10-03 is independent and can come second), then the head settings cards P10-05/06, the helper cards P10-07 ... P10-09, then the migration cards P10-10 ... P10-16 (each covers BOTH heads and the Presentation strings of one area; shared procedure in [migration-recipe.md](phase-10/migration-recipe.md); work list in `phase-10/inventory.md`, generated by P10-03), then P10-17 (guards) and P10-18 (docs).
- Cards that change what is on screen are checked by capturing both heads with `SQUILL_LANGUAGE=en` and `fr` (tools of P9-02); a screen that cannot be driven is listed in the card's report as "to check by hand".

### Phase 11 - Engine parity

Goal: every feature works fully and is **verified against a real server** on the four base engines (SQLite, PostgreSQL, MySQL, SQL Server); no new engine. Starting point (audit of 2026-11-03): the Testcontainers tests gated by `SQUILL_DOCKER_TESTS=1` never ran (no Docker; Podman 5.6.2 is installed); the profile has no TLS or timeout options and SQL Server trusts the certificate only for the literal host `localhost`; the drivers read the first result set only and drop server messages; the schema model has tables and views only (no routines, triggers, view definitions, materialized views) and the explorer lists the profile's database only; only a handful of column types were ever read or edited; the parser, completion and DDL generators were tested on crafted samples, never against statements a server accepts; the grid edit flow, several popups, the SSH tunnel and the terminal launch were never driven end to end; Avalonia still uses AvaloniaEdit (ADR 0002).

- Branch: implement phase 11 on the existing branch `phase-11-engine-parity`; do not create another branch.
- Order: P11-01 ... P11-05 first (containers and CI, so everything after can be tested for real), then the gaps by value: connection options P11-06 ... P11-09, cancel and timeout P11-10, result sets P11-11 ... P11-14, schema objects P11-15 ... P11-22, databases P11-23/24, then the per-feature verification cards P11-25 ... P11-35 (types, SQL corpus, designer, ER, export, grid edit, terminal, SSH), then one end-to-end card per engine P11-36 ... P11-39 (WinUI driven first, Avalonia smoke), last P11-40 (docs and support matrix). WinUI cards come before their Avalonia twins.
- Shared procedures (containers, verification report format, sample schemas): [verification-recipe.md](phase-11/verification-recipe.md).
- Cards that need a container use `rtk pwsh eng/run-docker-tests.ps1` (P11-01); a card must stop and report when Podman is unavailable and never mark Docker-gated tests as verified when they were skipped. Bugs found by verification cards that are too big for the card get a new card `P11-41` onwards (added to this index).

### Phase 12 - Release, installer and automatic updates

Goal: a user installs SQuilL with one `Setup.exe`, is told when a newer version exists, updates in two clicks without losing data, and a maintainer cuts a release by pushing a tag. Decisions (final, details in [release-recipe.md](phase-12/release-recipe.md) and in ADR 0009 written by P12-20): Velopack (installer, delta packages, feed on GitHub Releases of `squillapp/squill`), Windows WinUI head first (x64; arm64 only if Native AOT allows, P12-18), Avalonia Linux/macOS packaging last and optional (P12-21), channels `stable` and `beta` (Velopack channel `<channel>-<arch>`), one version file `eng/Version.props` with tag `v<version>`, no network call until the user chose a check frequency (one-time question, default `NotAsked`), nothing applied without a click, user data in `%APPDATA%\SQuilL` never touched by install, update or uninstall.

- Branch: implement phase 12 on the existing branch `phase-12-release`; do not create another branch. Never push a `v*` tag from a card (it would publish a real Release).
- Order: P12-01 first (Velopack under Native AOT: if its stop rule triggers, the Platform/engine cards are rewritten before anything else), then versioning and build P12-02 ... P12-06, then update logic P12-07 ... P12-09 (Platform), P12-10 ... P12-12 (Presentation), the heads P12-13 / P12-14 (WinUI) before P12-15 (Avalonia), the CI release P12-16 / P12-17 / P12-18, then verification P12-19 and docs P12-20, optional P12-21 last.
- Cards that package run in the background (an AOT publish takes minutes), never run an installer except P12-01 (throwaway id `SQuilLPoc`) and P12-19 (`SQuilLVerify`), and clean `artifacts/` afterwards. Bugs found by P12-19 too big for the card get `P12-22` onwards (added to this index).
