<!-- Release notes live here and nowhere else; eng/release-notes.ps1 extracts one section for the GitHub Release and the app. -->
<!-- Format: "## [Unreleased]", then "## [<SemVer>] - <yyyy-MM-dd>" with "### Added|Changed|Fixed|Removed" and "-" bullets. -->
<!-- Allowed markdown: ### headings, - bullets, paragraphs, **bold**, `code`, [text](https://...). No tables, images, HTML or nested lists. -->

# Changelog

## [Unreleased]

### Added

- MariaDB: `Explain` and `Analyze` show the execution plan (`EXPLAIN FORMAT=JSON` and `ANALYZE FORMAT=JSON`), the manual transaction state is read from the server, and MariaDB 11.4 joins the support matrix of the engine-parity record.
- Explorer filter popup, opened from an `n of m` badge on connection, database and schema rows or from the context menu: choose which databases, schemas and tables are shown, with wildcard patterns. It replaces the "Tables to show" dialog, is saved per connection, and filters saved by earlier versions are kept.
- Auto-refresh of a query tab: a clock menu before `Stop` re-runs the last read-only query every 5 s, 10 s, 30 s, 1 m, 5 m or a custom interval; it keeps the sort, filters, scroll and column widths, pauses on pending edits and stops on error.

### Fixed

- Pasting a very long text (100,000 characters or more) into a result grid, a cell editor or the value pane shows a `Pasting…` popup with a progress bar instead of freezing the window with no sign of life.

## [0.5.2] - 2026-11-20

### Fixed

- Updates: the choice At start and every hour now checks at every start, not only when the last check is more than 55 minutes old, so a release published a few minutes after the previous check is offered at the next launch.

## [0.5.1] - 2026-11-20

### Fixed

- WinUI explorer: clicking a database, schema or folder node highlights only that row instead of every expanded row below it.

## [0.5.0] - 2026-11-20

### Added

- Explorer: select several tables (Ctrl+click, Shift+click) and drop them in one action from the context menu or with `Delete`. The confirmation lists the tables, referencing tables are dropped first, a table outside the selection that still references one blocks the drop, and PostgreSQL, SQL Server and SQLite drop them all or none (MySQL stops at the first failure and says what was dropped).

### Fixed

- Dropping a table on a Production connection now asks the production confirmation and runs inside the open manual transaction (it used to run without either); MySQL asks about the implicit commit first.
- WinUI: the light theme now also applies to the command palette, the grid filter bar, the search boxes, the history status colors, the plan details panel and the splitters, even when Windows is dark.
- WinUI: the schema compare script pane redraws when its text changes; the privilege matrix shows its seven columns at 1440 px; the read-only marker under a filter, the export menu count and the CSV separator label are correct.
- WinUI and Avalonia: a completion popup with a single entry no longer stays open after `Ctrl+Enter` runs the query.
- Avalonia: the compare status bar shows the target environment after the sides are swapped; the privileges and sessions tabs no longer show the query view through them; `Annuler` stays visible in the toolbar with a transaction open at 1440 px.
- Home page: an Oracle connection card now has its engine badge and name, and a click on any card says "Connecting..." until the connection is open instead of looking unresponsive.
- WinUI explorer: names are no longer cut at the bottom, so leading underscores (`__EFMigrationsHistory`) and the tails of g, y and p show again.
- WinUI: the SQL editor now follows the light theme instead of staying dark.

## [0.4.1] - 2026-11-19

### Added

- Settings > Updates: a new choice, At start and every hour, checks for a new version at start (without delaying it) and then every hour while SQuilL stays open. The daily and weekly choices now also check while the app stays open.

## [0.4.0] - 2026-11-18

### Added

- Home page: when no tab is open and no connection is active, the main area shows the recent connections (`Ctrl+1` to `Ctrl+6`), quick actions, a tip and a search box that opens the command palette. A setting decides whether it shows at startup.
- Schema compare: compare the structure of two connected databases of the same dialect, see the differences side by side, and get an editable migration script to copy, save, open in the editor or run on the target (with the production confirmation).
- Sessions and locks: a monitor for PostgreSQL, MySQL and SQL Server with the sessions table, who blocks whom, cancel query and end session. It refreshes only while its tab is active.
- Roles and privileges (PostgreSQL): a matrix of the privileges of a role on tables and views, the generated `GRANT` and `REVOKE` statements, and Apply.
- Import: a CSV file wizard into an existing table, with a preview, column mapping and conversions, an error policy, a file of rejected rows and progress.
- Menu items, palette actions and explorer context items for each of these features, disabled with a reason when the connection or the engine does not support them.
- Grid: a filter bar with typed filters (equals, contains, greater than, is null, in a list, ...), a search box (`Ctrl+F`) and a column menu with sort and a filter by value. Filtering works on the loaded rows, makes the grid read-only until it is cleared, and does not change what Export writes.
- Command palette on `Ctrl+K` (or a click on the title bar search box): actions, tables and routines of every open connection, query history and go to line, with the matching letters highlighted. A history entry is inserted into the editor and never run.
- SSH tunnels check the host key of the jump host: the first key is shown with its fingerprint and remembered only if you trust it, and a changed key is refused with both fingerprints and the file to edit if the server was reinstalled.
- Table designer: a warning when a SQLite trigger uses a column that is renamed or dropped.

### Changed

- The query parameters prompt is now a panel under the editor instead of a dialog.
- WinUI explorer: row height and environment pills match the mockup.

### Fixed

- Names that are SQL keywords (a role or table called `user`, for example) are quoted in completion and in generated `GRANT` scripts.
- The sessions list and the privilege matrix say how many rows are not shown when there are more than 400.
- Home page: each recent connection shows its details line and open-tab count, and the new connection card has its subtitle. The Avalonia tip text wraps inside its card.
- WinUI privileges: the seven privilege columns fit at 1440 px, the legend is no longer cut off, and a role with one member reads in the singular. The sessions table columns are narrower so the wait and query columns are visible.
- WinUI: with the light theme chosen and a dark system theme, the editor, filter bar, plan table and splitters now follow the chosen theme; confirmation dialogs use the app theme; the production confirmation dialog has its danger frame and highlighted keywords.
- Schema compare: the Added, Modified and Removed filter chips scroll sideways instead of being cut off in the narrow list pane.
- Avalonia: the command palette footer hints no longer run into the result count.
- Typing in a long script is no longer slowed down by the SQL diagnostics, which now run in the background.
- The formatter keeps `TOP` and `WITH TIES` with their `SELECT`.
- A custom delimiter glued to the statement, such as `END$$`, now splits the script correctly.

## [0.3.0] - 2026-11-13

### Added

- Transactions: an Auto-commit / Manual toggle, a pending-changes pill, Commit and Rollback (`Ctrl+Alt+C`, `Ctrl+Alt+Z`) and a status bar segment, on SQLite, PostgreSQL, MySQL / MariaDB and SQL Server. Grid commits and designer applies run inside your open transaction, and closing, disconnecting or quitting asks what to do with it.
- Production confirmation: statements that modify data or schema on a Production connection ask first, with the estimated number of rows touched, and the same applies to Commit, grid commits and designer applies.
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
