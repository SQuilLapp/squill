<!-- Release notes live here and nowhere else; eng/release-notes.ps1 extracts one section for the GitHub Release and the app. -->
<!-- Format: "## [Unreleased]", then "## [<SemVer>] - <yyyy-MM-dd>" with "### Added|Changed|Fixed|Removed" and "-" bullets. -->
<!-- Allowed markdown: ### headings, - bullets, paragraphs, **bold**, `code`, [text](https://...). No tables, images, HTML or nested lists. -->

# Changelog

## [Unreleased]

### Added

- macOS: the menu moves to the system menu bar, with a SQuilL application menu (About, Settings, Check for updates, Hide, Quit) and Cmd shortcuts.
- macOS: a double click on the title bar zooms the window.

### Changed

- On a narrow window the Properties panel hides below 1280 px, toolbars scroll sideways and the designer's column table scrolls instead of squeezing its columns.

### Fixed

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
