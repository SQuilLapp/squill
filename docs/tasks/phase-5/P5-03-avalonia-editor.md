# P5-03 - Avalonia head: SQL editor on AvaloniaEdit

Status: done
Depends on: P5-02

## Goal
The interim editor for macOS/Linux (ADR 0002, point 1).

## Design decisions (final)
- First task: find an AvaloniaEdit build that restores against Avalonia 12.1.4 (`rtk dotnet package search AvaloniaEdit --prerelease`, also `Avalonia.AvaloniaEdit`). Pin it in `Directory.Packages.props`. If none restores, write `docs/adr/0004-avalonia-editor-fallback.md` and implement `TextBoxSqlEditor` (plain `TextBox`, no highlighting/completion) instead, then say so in the report; the card is still complete.
- `Editors/AvaloniaSqlEditor : ISqlEditor` wrapping `TextEditor`: highlighting through a `DocumentColorizingTransformer` fed by `SqlHighlighter.Classify` (colors from token resources: keyword, string, number, comment, function, parameter); completion with `CompletionWindow` fed by `ISqlCompletionProvider` through `CompletionFilter` on typing identifier characters, `.` or Ctrl+Space; diagnostics underlined via a second transformer from `SetDiagnostics`; hover tooltip from the hover provider; selection/caret mapped to `ISqlEditor`.
- `App` uses it as `EditorFactory`. Re-highlighting is done for the visible lines only.

## Out of scope
- Folding, find/replace UI, multi-caret.

## Tasks
- [x] Implement the editor (or the fallback) and register it in the composition.
- [x] Headless test: set text, verify `Text`, `CaretOffset`, `Selection` mapping, and that a completion request is made for `select * from ` with a fake provider.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).
- `rtk dotnet publish src/Squill.App.Avalonia -c Release -r win-x64` succeeds with zero IL2xxx/IL3xxx warnings.

## Suggested commit message
Add AvaloniaEdit-based SQL editor
