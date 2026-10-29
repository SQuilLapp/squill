# P5-09 - WinUI head: plain-text SQL editor

Status: todo
Depends on: P5-08

## Goal
The interim `ISqlEditor` for WinUI until LoomEditor is integrated (ADR 0002, spike).

## Design decisions (final)
- `Editors/PlainSqlEditor : ISqlEditor` over a multi-line `TextBox` (`AcceptsReturn`, monospace token font, no wrapping): text, caret and selection mapped; completion via a `ListView` in a `Popup` anchored under the caret line (position approximated from caret line and column with a fixed line height); diagnostics shown as a count in a small overlay (no squiggles); hover provider unused. No highlighting.
- `App` uses it as `EditorFactory`. This class stays as the fallback when LoomEditor is not available (P5-13 keeps it).

## Out of scope
- Highlighting (LoomEditor).

## Tasks
- [ ] Implement the editor.
- [ ] Verify manually by running the app (no WinUI test project exists; logic worth testing belongs in `Squill.Presentation`).

## Acceptance criteria
- `rtk dotnet build Squill.Windows.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk dotnet publish src/Squill.App.WinUI -c Release -r win-x64` succeeds with zero IL2xxx/IL3xxx warnings.
- The card's report describes the manual check (typing, selection, completion popup).

## Suggested commit message
Add WinUI plain-text SQL editor
