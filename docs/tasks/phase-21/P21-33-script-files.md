# P21-33 - Platform and Presentation: open and save .sql script files, tabs bound to files, recent files

Status: todo
Depends on: none

## Goal
A query tab can be opened from and saved to a `.sql` file, like any editor: `Ctrl+O`, `Ctrl+S`, `Ctrl+Shift+S`, a dirty mark, recent files.

Exists today: query tabs hold text only (not saved; closing loses it after the usual question if any); `IDialogService.PickOpenFileAsync` / `PickSaveFileAsync`; the compare view saves a `.sql` (`compare.saveSql`); no file is bound to a tab.

## Design decisions (final)
- `QueryTabViewModel.FilePath` (null for a scratch tab), `IsDirty` (text differs from the last saved/loaded text), title = file name; `SaveCommand`, `SaveAsCommand`, `WorkspaceViewModel.OpenScriptCommand`.
- Encoding: read with BOM detection (UTF-8 default), write UTF-8 without BOM, keep the file's line endings (detected on load).
- A file changed on disk while open and not dirty reloads silently; dirty -> a banner `Reload` / `Keep mine` (a `FileSystemWatcher` per open file, debounced).
- Closing a dirty file tab asks `Save`, `Don't save`, `Cancel`; the exit path (`PrepareExitAsync`) includes dirty tabs.
- Recent files (last 20) in a new `IRecentFilesStore` (JSON, Platform), shown in the File menu and on the home page recent list.
- The connection of a file tab is the active connection when opened; a `-- squill:connection=<profile name>` first-line comment is **not** used (no magic comments).
- Heads: menu items and keys in both heads are part of this card (small: menu entries, dirty dot on the tab).

## Out of scope
- Files panel and bookmarks (P21-34), Git (P21-37).

## Tasks
- [ ] Tests: load/save round trip with BOM and CRLF, dirty tracking, external change reload and conflict, close and exit questions, recent store.
- [ ] Both heads: `Ctrl+O`, `Ctrl+S`, `Ctrl+Shift+S`, the dirty dot; drive both exes (recipe 3; type the path where the picker cannot be driven).
- [ ] README section "Script files" (short), `CHANGELOG.md` line.

## Acceptance criteria
- Recipe 2 and 3 acceptance for both heads.

## Risks
- `FileSystemWatcher` on network drives is unreliable: also compare the timestamp when the tab gets focus.

## Open points (defaults)
- None.

## Suggested commit message
Open and save SQL script files in query tabs
