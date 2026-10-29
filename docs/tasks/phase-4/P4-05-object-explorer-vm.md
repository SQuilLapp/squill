# P4-05 - Presentation: object explorer view-model

Status: todo
Depends on: P4-04

## Goal
A tree of schemas, tables, views and columns built from the session's schema, with a filter.

## Design decisions (final)
- Namespace `Squill.Presentation.Explorer`. `ExplorerNode : ViewModelBase { string Title; string? Detail; ExplorerNodeKind Kind; ObservableCollection<ExplorerNode> Children; bool IsExpanded; bool IsVisible }`; `enum ExplorerNodeKind { Schema, Table, View, Column, Index, ForeignKey, Folder }`.
- `ObjectExplorerViewModel(ConnectionSession session)`: `Roots`; schemas appear as nodes only when the snapshot has more than one schema (otherwise tables are roots); a table node has folders `Columns`, `Indexes`, `Foreign keys`; column `Detail` = data type plus ` PK`/` NULL` markers; `FilterText` hides non-matching leaves and keeps their ancestors visible (case-insensitive contains on `Title`); `RefreshCommand` calls `session.RefreshSchemaAsync` and rebuilds; event `OpenTableRequested(TableInfo)`; `ActivateCommand(ExplorerNode)` raises it for table/view nodes.
- `SqlSnippets.SelectTop(TableInfo t, DatabaseDialect d, int n = 100)` in `Squill.Presentation.Editor`: `SELECT TOP 100 * FROM [dbo].[t]` for SQL Server, `SELECT * FROM "s"."t" LIMIT 100` otherwise; identifier quoting per dialect (`"x"`, `` `x` ``, `[x]`, doubling the closing quote char).

## Out of scope
- Context menus, DDL scripting.

## Tasks
- [ ] Implement the view-model and `SqlSnippets`.
- [ ] Tests: single vs multiple schemas, filter behaviour, quoting with embedded quotes, `SelectTop` per dialect, refresh rebuilds.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add object explorer view-model
