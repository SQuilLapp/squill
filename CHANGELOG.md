<!-- Release notes live here and nowhere else; eng/release-notes.ps1 extracts one section for the GitHub Release and the app. -->
<!-- Format: "## [Unreleased]", then "## [<SemVer>] - <yyyy-MM-dd>" with "### Added|Changed|Fixed|Removed" and "-" bullets. -->
<!-- Allowed markdown: ### headings, - bullets, paragraphs, **bold**, `code`, [text](https://...). No tables, images, HTML or nested lists. -->

# Changelog

## [Unreleased]

## [0.3.0] - 2026-11-13

### Added

- Transactions: an Auto-commit / Manual toggle, a pending-changes pill, Commit and Rollback (`Ctrl+Alt+C`, `Ctrl+Alt+Z`) and a status bar segment, on SQLite, PostgreSQL, MySQL / MariaDB and SQL Server. Grid commits and designer applies run inside your open transaction, and closing, disconnecting or quitting asks what to do with it.
- Production confirmation: statements that modify data or schema on a Production or Staging connection ask first, with the estimated number of rows touched, and the same applies to Commit, grid commits and designer applies.
- Explorer: every database of a connection can be opened, each one can be refreshed from its context menu, and a Keys folder lists the primary and unique keys.
- Explorer: choose which tables and views to show, from the context menu of a database or schema or directly on a table (like DataGrip). The choice is saved per connection and database; Show all tables clears it.
- Oracle: Explain and Analyze open a plan tab from `EXPLAIN PLAN` and `DBMS_XPLAN`, with the actual plan from the run statistics.
- Grid: the cell editor follows the column type (numbers refuse letters and step with the arrow keys, booleans cycle through true, false and NULL, dates step by day) and checks the value while you type.
- Grid: a popup picks true, false or NULL, a date and time popup opens from a button next to the editor, and a DEFAULT choice gives the cell the default value of its column.
- Results: a long, multi-line or JSON value selected in the grid opens a resizable preview pane on the right with the full text (JSON indented), editable and validated when the cell can be edited.

### Changed

- Explorer: tables and views are sorted A to Z, with names starting with an underscore first.
- The properties panel on the right is resizable with a splitter, like the left panel.
- SQuilL runs one instance per user: starting it a second time shows the running window.
- Built on .NET 11 GA instead of RC3.

### Fixed

- Explorer: only the first database of a connection could be expanded.
- The environment colour (Development, Staging, Production) now follows the tab on screen, including in the query view, instead of the explorer selection.
- PostgreSQL: jsonb operators (`#>`, `#>>`, `#-`, `?`, `?|`, `?&`, `@?`, `@@`) are no longer flagged as errors in the editor.

## [0.2.0] - 2026-11-10

### Added

- Query execution plan: Explain and Analyze open a plan tab (tree, graph and text views, relative cost, estimated and actual rows, warnings) on SQLite, PostgreSQL, MySQL and SQL Server.
- SQL completion: after `JOIN` the popup offers each related table with its alias and the `ON` condition built from the foreign keys (one row per key, composite keys, self joins), and the popup opens by itself after `JOIN `.
- SQL completion: statement snippets (`sel`, `ins`, `upd`, `del`, `cte`, `crt`), documentation text on items, functions in the popup, and the keyword case setting is followed.
- macOS: the menu moves to the system menu bar, with a SQuilL application menu (About, Settings, Check for updates, Hide, Quit) and Cmd shortcuts.
- macOS: a double click on the title bar zooms the window.

### Changed

- SQL completion: items are ranked by keys, usage and relatedness, and the join keywords offered follow the engine (no `FULL JOIN` on MySQL).
- On a narrow window the Properties panel hides below 1280 px, toolbars scroll sideways and the designer's column table scrolls instead of squeezing its columns.

### Fixed

- SQL completion: tables outside the default schema are inserted schema-qualified, and a column name shared by two tables is inserted qualified.
- Texts drawn over each other or cut without an ellipsis in the table designer, the toolbars, the connection editor and Settings.

## [0.1.0] - 2026-11-09

### Added

- SQL client for SQLite, PostgreSQL, MySQL / MariaDB and SQL Server, built on .NET 11 with Native AOT.
- WinUI 3 head for Windows and Avalonia head for macOS and Linux, sharing one view-model layer.
- SQL editor with syntax highlighting, formatting, snippets and `@parameter` prompts, plus an integrated terminal.
- Schema explorer showing tables, views and routines, with the definition script of each object.
- Result grid with export, display formats and large-result handling.
- Inline data editing on plain `SELECT` results: edits are previewed as SQL and committed in one transaction.
- Schema designer for columns, indexes and foreign keys, with the generated DDL previewed before it runs.
- ER diagram per schema with SVG export and remembered box positions.
- Saved connections with secrets in the OS store, TLS options and SSH tunnels.
- Query history.
- French and English interface, switchable in Settings, with light and dark themes.
- Engine parity across the four databases, checked against real servers.
- Windows installer (`Setup.exe`) and automatic updates from GitHub Releases: stable and beta channels, delta downloads, applied only after a click; your data in `%APPDATA%\SQuilL` is kept on update and uninstall.
