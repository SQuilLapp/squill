# P21-34 - Presentation: Files panel (script folders, favorites) and line bookmarks

Status: todo
Depends on: P21-33, P21-27

## Goal
A `Files` panel on the left rail (next to Explorer, History, Snippets) lists script folders the user added, with favorites on top; editor line bookmarks jump between places; rename refactoring (P21-27) can also edit these files.

Exists today: the left rail with `SidebarPanel` (`Explorer`, `History`, `Snippets`), `SnippetsViewModel`, `HistoryViewModel`; script files from P21-33.

## Design decisions (final)
- `FilesPanelViewModel`: roots (folders added by the user, stored in settings `ScriptFolders`; default one root `Documents/SQuilL scripts` created on first use only), a lazy tree of folders and `*.sql` files, filter box, `New file`, `New folder`, `Rename`, `Delete` (to the OS recycle bin through the platform service; never permanent), `Open` (P21-33), `Reveal in file manager`.
- Favorites: a pinned list (files or folders) at the top, stored in settings `FavoriteScripts`.
- Bookmarks: `QueryTabViewModel.Bookmarks` (line numbers, kept on edit by shifting with line inserts/removes), `F11` toggles, `Shift+F11` lists them in the palette (`PaletteActionRegistry`), saved per file in `bookmarks.json` (Platform store) for file tabs, in memory for scratch tabs. `ISqlEditor.SetBookmarks(lines)` (default no-op until the heads draw them).
- `IRenameSource` for script files under the roots (P21-27 scope `Script files`): a file open and dirty in a tab is edited in the tab, otherwise on disk.

## Out of scope
- Heads (P21-35, P21-36), Git (P21-37).

## Tasks
- [ ] Tests over a temp folder: tree, filter, create/rename/delete (recycle bin through a fake), favorites, bookmarks shift on edits and persistence, rename source.

## Acceptance criteria
- Recipe 2 common acceptance.

## Risks
- Large folders: list lazily, cap a folder at 5,000 entries with a notice.

## Open points (defaults)
- Default root folder name `SQuilL scripts` under the user's Documents.

## Suggested commit message
Add the Files panel with favorites and editor line bookmarks
