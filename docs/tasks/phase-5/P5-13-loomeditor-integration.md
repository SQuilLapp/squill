# P5-13 - WinUI head: LoomEditor integration

Status: done
Depends on: P5-12, P3-15

## Goal
Replace the plain editor with LoomEditor when available (ADR 0002, risk R1) and prove 100,000-line behaviour.

## Design decisions (final)
- Read the LoomEditor sources first (checkout `D:\LoomEditor`, or a path from `Directory.Build.local.props`). If the checkout or a buildable control is missing, STOP and report; do not guess its API.
- Reference by MSBuild property: `Directory.Build.local.props` (git-ignored; add to `.gitignore`) may define `LoomEditorPath`; `Squill.App.WinUI.csproj` adds the `ProjectReference` and defines `SQUILL_LOOMEDITOR` only when the property is set. Without it the head builds with `PlainSqlEditor` (the CI path).
- `Editors/LoomSqlEditor : ISqlEditor` (compiled under `SQUILL_LOOMEDITOR`): text/caret/selection mapping; register the four SQL grammars of `assets/grammars/` with the TextMate engine if it accepts external grammars, else document the gap in ADR 0002 'Still open' and use `SqlHighlighter` decorations; adapters from `ISqlCompletionProvider` to LoomEditor's completion provider interface, hover and definition-free; diagnostics as decorations/markers; registered in `LanguageFeatureRegistry`. Signature help and formatting are NOT in LoomEditor: format via `FormatDocumentCommand`, signature help is deferred (note in ADR 0002).
- `--editor-demo` flag: opens the editor with a generated 100,000-line SQL file, prints time to first paint and time to apply highlighting, triggers a completion request programmatically and prints its latency; record in `docs/poc/editor.md`. Criterion: highlighting + completion work on 100,000 lines without freezing (UI thread stall < 100 ms measured with a `DispatcherQueueTimer` heartbeat).

## Out of scope
- Changes inside LoomEditor itself (file requests in `docs/poc/editor.md` instead).

## Tasks
- [x] Wire the optional reference and the editor adapter.
- [x] Run `--editor-demo`, write `docs/poc/editor.md` with numbers and the list of missing LoomEditor APIs.

## Acceptance criteria
- `rtk dotnet build Squill.Windows.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.Windows.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).
- `rtk dotnet build Squill.Windows.slnx -c Release -warnaserror` passes both with and without `LoomEditorPath` set.
- `docs/poc/editor.md` exists.

## Suggested commit message
Add optional LoomEditor integration for the WinUI head
