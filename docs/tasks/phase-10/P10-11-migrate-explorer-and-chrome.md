# P10-11 - Migrate strings: explorer, menus, window chrome and tabs

Status: done
Depends on: P10-03, P10-04, P10-07, P10-08

## Goal
The object explorer, its context menus, the menu bar, title bar, activity rail, sidebar, tab strip, properties panel and main windows are fully bilingual in both heads.

## Design decisions (final)
Follow `docs/tasks/phase-10/migration-recipe.md`. Area `explorer-chrome` of `inventory.md`.
- Files in scope (the inventory is authoritative): `src/Squill.Presentation/Explorer/*` (folder names such as "Foreign keys", "Indexes", "Columns", counts through `FormatPlural` where a noun follows), `Workspace` tab titles built in Presentation for connections/new tabs ("Query 1", "New table", "Terminal": the number stays, the word is a catalog key), `MainWindow.axaml(.cs)` / `MainWindow.xaml(.cs)`, `SidebarView(.axaml/.xaml)`, `ObjectExplorerView`, `PropertiesPanel`, `WorkspaceTabList`, `StatusBar` is NOT here (P10-12), context menus and flyouts of the explorer, window titles, tray/tooltips of the rail, `ExplorerGlyphs` tooltips.
- Explorer node text that is a database object name (table, column, schema) is data and stays as is; only the group/folder captions, badges and tooltips are translated.
- Menu accelerator text (`Ctrl+N`) is not translated; mnemonics are not used.
- The command-search box placeholder already exists (`search.placeholder`); verify it is the one used by both heads.

## Out of scope
- Query tab content and status bar (P10-12), history / snippets panels (P10-14).

## Tasks
- [x] Migrate every `explorer-chrome` inventory line and tick it.
- [x] Tests per recipe rule 7 (explorer folder captions and counts in both languages, tab titles).
- [ ] English captures: main window, explorer with a connected SQLite sample database (`tools/visual/` sample data), every menu and the explorer context menu, both heads. PARTLY DONE: the WinUI main window was captured in English (menu bar, search box, explorer header, filter, status bar all English); menus, the connected explorer and context menus cannot be driven from the agent session, to check by hand with `SQUILL_LANGUAGE=en` (see `tools/visual/README.md`), both heads. Avalonia not captured.

## Acceptance criteria
- Common criteria of the recipe.

## Suggested commit message
Localize the explorer, menus and window chrome
