# P20-05 - Presentation: one drop flow for one or many tables, through the runner, the transaction and the production confirmation

Status: done
Depends on: P20-04

## Goal
`WorkspaceViewModel.DropTableAsync` (`src/Squill.Presentation/Workspace/WorkspaceViewModel.cs`, near line 786) confirms with a plain dialog and then calls `session.Connection.ExecuteAsync` for each statement. It does **not** ask the production confirmation (a `DROP TABLE` on a Production profile runs after one click), does not use `SqlRunner.BeginScopeAsync` (so inside an open user transaction it is not journaled or savepointed, and outside one it is not atomic), and does not ask the implicit-commit question on MySQL. Build the one flow that drops one or many tables like every other write (recipe 4 of [verification-recipe.md](verification-recipe.md)), and make the single drop use it.

## Design decisions (final)
- `WorkspaceViewModel` gets `[RelayCommand] DropTablesAsync(IReadOnlyList<TableInfo>? tables)` on `ActiveSession`; the existing `DropTableCommand` (`TableInfo?`) stays (its callers and tests) and calls the same private method with a one-item list. The explorer events that drive them are P20-06.
- Flow (mirror `TableDesignerViewModel.ApplyAsync`, `src/Squill.Presentation/Designer/TableDesignerViewModel.cs` lines 227-300, for the order of questions and the scope handling):
  1. Keep only `TableKind.Table`; empty -> return. `plan = TableDropPlan.Build(tables, session.Schema)`.
  2. `plan.Blockers` not empty -> `ShowErrorAsync(workspace.dropBlocked title, message)` listing each `referencing -> referenced` (at most 10, then `prod.more`-style "and n more"), **nothing is sent**, return.
  3. `statements` = `DdlGenerators.For(dialect).DropTable(schema, name)` of every table of `plan.Order`, flattened.
  4. `requirement = ProductionGuard.Evaluate(profile, statements)`. When `requirement.Required` skip the plain confirmation (the production dialog lists the statements and asks to type the database name; `DROP` is destructive); otherwise `ConfirmAsync(title, message)`: with one table the existing texts (`workspace.dropTable` / `workspace.dropTableConfirm`, so the existing tests keep passing), with several `workspace.dropTables` / `workspace.dropTablesConfirm` listing the qualified names one per line (at most 15 lines then "and n more").
  5. Non-transactional DDL (`!DdlGenerators.UsesTransactionalDdl(dialect)`) and `tx.State == TransactionState.Open` -> the same `tx.implicitCommit.*` question as the designer; no -> return.
  6. `ProductionRequestBuilder.ConfirmWriteAsync(_dialogs, profile, ConfirmationKind.SchemaChange, statements, tables.Count, label, tx.Mode)` where `label` is the table name or `UiText.FormatPlural("workspace.dropTablesLabel", n)`; false -> return. (When step 4 already asked the plain confirmation this is a no-op outside Production.)
  7. `using var operation = tx.BeginOperation();` then, for a transactional dialect (PostgreSQL, SQL Server, SQLite), one `scope = await SqlRunner.BeginScopeAsync(...)`; run each statement with `SqlRunner.ExecuteAsync`, `scope.Observe(statement, rows)`, `scope.CompleteAsync` at the end; on any exception `scope.FailAsync()` (all or nothing) and the error dialog. For a non-transactional dialect run the statements one by one without a scope, stop at the first failure, and show `workspace.dropPartial` (count dropped of total, the first failing table, the engine message) so the user knows what is gone.
  8. In every outcome after statements ran: `session.RefreshSchemaAsync`, `Explorer.Reload(session)`; a refresh failure must not hide the drop result. Designers of dropped tables are closed (existing `Matches` logic, now for each dropped table; on a rolled-back failure nothing is closed).
- Catalog (fr and en, parity test): `workspace.dropTables`, `workspace.dropTablesConfirm`, `workspace.dropTablesLabel.one|.other`, `workspace.dropBlocked`, `workspace.dropBlockedMessage`, `workspace.dropPartial`. French uses typographic spaces like the neighbors; read `UiTextCatalogFr.cs` lines near 88.

## Out of scope
- Selection (P20-06), views, `CASCADE`, closing table tabs of dropped tables (the designer tabs are closed today; keep that, do not add more).

## Tasks
- [x] Tests first, `tests/Squill.Presentation.Tests/Workspace/WorkspaceDropTablesTests.cs` using the fixtures of `WorkspaceDesignerTests.cs` (read it: fake provider with `Executed`, `ExecuteFailure`, `FakeDialogService`); run them, see them fail: (a) **Production profile, one table: `ConfirmProductionAsync` is asked with the `DROP TABLE` text and, declined, nothing runs** (fails today: the existing single drop never asks); (b) Production typing required; (c) three tables with `commandes` -> `clients` run in dependency order; (d) a blocker outside the selection: error, `Executed` empty; (e) declined plain confirmation: nothing runs; (f) PostgreSQL dialect, third statement fails: `BEGIN` ... `ROLLBACK`, error shown, schema not reloaded as changed; (g) MySQL dialect, second statement fails: first ran, `workspace.dropPartial` shown, no `BEGIN`; MySQL with an open transaction asks the implicit-commit question; (h) open user transaction (Manual mode) on PostgreSQL: a `SAVEPOINT` instead of `BEGIN`, journal holds the statements, no `COMMIT`; (i) the existing single-drop tests unchanged and green.
- [x] Implement the flow and the catalog entries.

## Acceptance criteria
- Recipe 2 common acceptance; the single-drop tests of `WorkspaceDesignerTests` pass unchanged.

## Risks
- Oracle: `DdlGenerators.UsesTransactionalDdl` returns true for everything but MySQL, but Oracle DDL commits implicitly. Do not change that method here (the designer relies on it): if the Oracle bridge makes `BEGIN` fail or misbehave, stop with a report and propose a card; note the behavior in the Done notes.
- SQLite `DROP TABLE` inside a transaction with `PRAGMA foreign_keys` on checks the rows: the order from P20-04 is what makes it pass.
- The production dialog lists at most 5 statements (`ProductionRequestBuilder.Listed`): that is existing behavior, do not change it.

## Suggested commit message
Drop one or several tables through the runner, the session transaction and the production confirmation

## Done notes
- `DropTablesCommand` added; `DropTableCommand` calls the same flow. Tests in `WorkspaceDropTablesTests` (10), single-drop tests unchanged.
- Catalog: the two `workspace.dropTablesLabel.*` keys read the same in both languages and are on the parity allow list.
- A rolled-back drop still reloads the schema (harmless) and closes no designer. Oracle not driven: the flow treats it as transactional like the designer does.
- Full test run: one Avalonia timing test (`StatusBar_TransactionSegment_FollowsTheSessionTransaction`) failed once and passed alone.
