# P5-05 - Avalonia head: connection dialog and list

Status: todo
Depends on: P5-04

## Goal
Create, edit, delete, test and open connections from the UI.

## Design decisions (final)
- `ConnectionEditorWindow` (modal dialog) bound to `ConnectionEditorViewModel`: fields shown per dialect (SQLite shows only name + file with browse button via `StorageProvider`), SSH section collapsible, password boxes masked, validation messages inline, Test and Save buttons; `AvaloniaDialogService` gains real implementations of `PickSaveFileAsync` through `StorageProvider.SaveFilePickerAsync`.
- `SidebarView` connection list: double-click or Enter connects (`ConnectCommand`), context menu New/Edit/Delete/Disconnect, connected sessions marked with a token-colored dot; empty state with a `New connection` button; connection errors shown through `ShowErrorAsync`.

## Out of scope
- Import from other tools.

## Tasks
- [ ] Implement dialog and list interactions.
- [ ] Headless tests: editor window shows the SSH section only when `UseSsh`; list context menu commands are bound (check `Command` bindings are non-null).

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).
- `rtk dotnet publish src/Squill.App.Avalonia -c Release -r win-x64` succeeds with zero IL2xxx/IL3xxx warnings.

## Suggested commit message
Add Avalonia connection dialog and list
