# P7-21 - Presentation: table designer view-model

Status: done
Depends on: P7-08, P7-10, P7-11, P7-12, P7-13, P7-14, P4-04

## Goal
The headless view-model that edits a `TableDesign` (new table or existing one), shows validation issues and the generated script, and applies it to the database.

## Design decisions (final)
- Namespace `Squill.Presentation.Designer`, folder `src/Squill.Presentation/Designer/`. `TableDesignerViewModel : ViewModelBase` created with `(ConnectionSession session, TableInfo? existing, IDialogService dialogs, IUiDispatcher dispatcher)`; `existing == null` means "new table" (default name `new_table`, schema = `session.Schema.DefaultSchema`, one starter column `id` of the first catalog integer type, auto-increment, primary key); otherwise the draft starts from `TableDesign.FromTable(existing)` and that design is kept as `Original`. `Title` = `Design <name>` or `New table`.
- Row view-models (all `ViewModelBase`, observable properties, in the same folder): `ColumnRowViewModel` (`OriginalName`, `Name`, `DataType`, `IsNullable`, `IsPrimaryKey`, `DefaultExpression`, `IsAutoIncrement`; setting `IsPrimaryKey = true` sets `IsNullable = false`), `IndexRowViewModel` (`OriginalName`, `Name`, `IsUnique`, `ColumnsText` = comma separated column names), `ForeignKeyRowViewModel` (`OriginalName`, `Name`, `ColumnsText`, `ReferencedSchema`, `ReferencedTable`, `ReferencedColumnsText`, `OnDelete`, `OnUpdate`). Collections `Columns`, `Indexes`, `ForeignKeys` are `ObservableCollection`s; commands `AddColumnCommand`, `RemoveColumnCommand(row)`, `MoveColumnUpCommand(row)`, `MoveColumnDownCommand(row)`, `AddIndexCommand`, `RemoveIndexCommand(row)`, `AddForeignKeyCommand`, `RemoveForeignKeyCommand(row)`. `TypeSuggestions` = `DataTypeCatalog.For(session.Profile.Dialect)`; `ReferentialActions` = the enum values.
- Editing properties: `Schema`, `Name`. Any change to a row or property rebuilds `Design` (`TableDesign` from the rows: primary key = rows with `IsPrimaryKey` in row order; `ColumnsText` split on `,` and trimmed, empty entries dropped), then `Issues` (`DesignValidator.Validate(Design, Original, dialect)` plus one extra **error** per foreign key whose referenced table is missing from `session.Schema` and is not the table itself, plus one **warning** "views depending on this table are not recreated" when the dialect is SQLite, `Original != null` and the snapshot contains a view), then `Script` (`DdlGenerators.ToScript(...)`, empty when any error exists; `CreateTable` for a new table, `Alter` otherwise) and `HasChanges`. Recomputation is synchronous and cheap; no debounce.
- `ApplyCommand` (can execute when `HasChanges` and no error): when warnings exist, `dialogs.ConfirmAsync("Apply changes", <warnings joined by newline>)` first. Then run the statements of `Script` one by one through `session.Connection.ExecuteAsync(sql, [], ct)`, draining and disposing the result reader the way `QueryTabViewModel` does. Transaction rule: when `DdlGenerators.UsesTransactionalDdl(dialect)`, statements are bracketed with `BEGIN` / `COMMIT` (`BEGIN TRANSACTION` / `COMMIT TRANSACTION` for SQL Server); **leading and trailing statements starting with `PRAGMA` run outside the bracket** (SQLite rebuild, P7-14). On a failure: `ROLLBACK` (swallow its own errors), `dialogs.ShowErrorAsync("Apply failed", message)`, keep the draft. For MySQL there is no bracket; the error message adds "MySQL does not roll back DDL: statements before the failing one were applied" and the schema is refreshed anyway. On success: `session.RefreshSchemaAsync`, reload `Original` and the rows from the refreshed table (found by the new schema and name), `StatusText` = "Applied N statement(s)".
- `RevertCommand` restores the rows from `Original` (or the starter design). `OpenScriptCommand` raises event `ScriptRequested(string script)` (consumed by the workspace, P7-22). Cancel support: a `CancellationTokenSource` cancelled by `CancelCommand`, same pattern as the query tab.
- No UI types, no reflection.

## Out of scope
- Views, tabs and workspace commands (P7-22 and heads), dropping tables (P7-22), data migration beyond what the generators emit.

## Tasks
- [x] Implement the view-models.
- [x] Tests in `tests/Squill.Presentation.Tests` with a fake `IDatabaseConnection` recording statements (see the fakes used by the P4 tests): new-table starter, edits rebuild `Design`/`Script`, a validation error blocks `ApplyCommand`, primary key flag forces non-null, missing referenced table error, apply order and transaction bracket per dialect (PostgreSql, SqlServer, Sqlite with `PRAGMA` outside, MySql without), failure triggers `ROLLBACK` and keeps the draft, MySQL failure message, warnings require confirmation (declined = nothing executed), success refreshes the schema and resets `HasChanges`, revert, `ScriptRequested`.
- [x] One end-to-end test with the real SQLite provider on a temp file: create a table through the view-model, then rename a column and change a type through the rebuild path; rows survive.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add table designer view-model
