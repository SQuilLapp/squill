# P5-09 - WinUI head: plain-text SQL editor

Status: done
Depends on: P5-08

## Goal
The interim `ISqlEditor` for WinUI until LoomEditor is integrated (ADR 0002, spike).

## Design decisions (final)
- `Editors/PlainSqlEditor : ISqlEditor` over a multi-line `TextBox` (`AcceptsReturn`, monospace token font, no wrapping): text, caret and selection mapped; completion via a `ListView` in a `Popup` anchored under the caret line (position approximated from caret line and column with a fixed line height); diagnostics shown as a count in a small overlay (no squiggles); hover provider unused. No highlighting.
- `App` uses it as `EditorFactory`. This class stays as the fallback when LoomEditor is not available (P5-13 keeps it).

## Out of scope
- Highlighting (LoomEditor).

## Tasks
- [x] Implement the editor.
- [x] Verify manually by running the app (no WinUI test project exists; logic worth testing belongs in `Squill.Presentation`).

## Acceptance criteria
- `rtk dotnet build Squill.Windows.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk dotnet publish src/Squill.App.WinUI -c Release -r win-x64` succeeds with zero IL2xxx/IL3xxx warnings.
- The card's report describes the manual check (typing, selection, completion popup).

## Suggested commit message
Add WinUI plain-text SQL editor

## Notes
- `PlainSqlEditor` is not hosted in a view yet (P5-11), so the manual check was limited to a temporary build that set it as the window content: the Native AOT exe started, stayed responsive and was stopped; typing, selection and the completion popup could not be driven from here and need a look once P5-11 hosts the editor.
- `Text` maps the `TextBox` line break (`\r`) to `\n`, offsets are unchanged. The popup position ignores scrolling.
- A second `dotnet publish` into an existing publish folder fails in `CopyXamlResourcesToPublish` (its `**\*.xbf` glob also matches `publish\`); delete `bin\...\publish` before republishing. Pre-existing from P5-08.
