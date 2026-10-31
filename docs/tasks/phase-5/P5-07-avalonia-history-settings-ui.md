# P5-07 - Avalonia head: history, snippets and settings views

Status: done
Depends on: P5-06

## Goal
Expose the remaining view-models.

## Design decisions (final)
- Sidebar gets a segmented switch `Explorer | History | Snippets` (keeping the explorer alive); `HistoryView` (filter box, list with SQL preview, time, duration, success marker, double-click opens), `SnippetsView` (list, Save current, Insert, Delete via context menu).
- `SettingsWindow` (Ctrl+,) bound to `SettingsViewModel`: theme radio buttons, font size numeric, max grid rows numeric, keyword case combo; changes apply immediately (font size to the editor, theme variant to the app).
- A status-bar link opens the data directory (`AppPaths.DataDirectory`) in the system file manager.

## Out of scope
- Keyboard shortcut customization.

## Tasks
- [x] Implement views and wiring.
- [x] Headless tests: switching the segmented control changes the visible panel; settings window binds the four properties.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).
- `rtk dotnet publish src/Squill.App.Avalonia -c Release -r win-x64` succeeds with zero IL2xxx/IL3xxx warnings.

## Suggested commit message
Add Avalonia history, snippets and settings views
