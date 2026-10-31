# Editor: LoomEditor integration (P5-13)

Risk R1: the SQL editor must stay responsive on very large scripts. The WinUI head uses the owner's LoomEditor (ADR 0002) through `ISqlEditor`.

## Wiring

- `Directory.Build.local.props` (git-ignored, imported by `Directory.Build.props`) sets `LoomEditorPath` (here `D:\LoomEditor`). Only then does `Squill.App.WinUI.csproj` add the `ProjectReference` to `LoomEditor.WinUI`, define `SQUILL_LOOMEDITOR` and move the head to `net11.0-windows10.0.26100.0` (LoomEditor targets the 26100 Windows SDK and a project cannot reference a higher Windows TFM). Without the property (CI) the head builds with `PlainSqlEditor`.
- `EditorFactory.Create()` returns `LoomSqlEditor` under `SQUILL_LOOMEDITOR`, else `PlainSqlEditor`; both implement `IEditorControl` so `QueryTabView` places either.
- `LoomSqlEditor` maps text, caret and selection through `TextModel.GetOffsetAt` / `GetPositionAt` (text is kept `\n`-only), completion and hover through `ICompletionItemProvider` / `IHoverProvider` registered in `LanguageFeatures`, diagnostics through `Markers.Change`.
- Completion and hover send the provider only the statement at the caret (`SqlStatementSplitter`); parsing the whole 100,000-statement script took more than a minute per request.
- Highlighting: `SqlHighlighter` runs on a worker thread over the whole text and the result is drawn as decorations (`CustomDecorationStyles`, light and dark colours) for the lines around the viewport only, re-applied on scroll. Decorating all 2,000,000 spans made every update cost O(total) and starved the UI thread (heartbeat gaps of 60-110 ms with 1,000-4,000 spans per chunk, 2.3 s without yielding).

## `--editor-demo`

`Squill.App.WinUI.exe --editor-demo` (Native AOT publish built with `LoomEditorPath`, stdout redirected, the app exits by itself) loads `SqlDemoScript.Generate(100_000)` (one statement per line, 100,000 lines, about 7 MB), waits for the highlighting, jumps the scroll 50 times, then asks for completion at the caret in the middle of the file. A 16 ms `DispatcherQueueTimer` heartbeat records the gaps between its ticks (a gap over 16 ms is a UI stall).

| Metric | Value |
|---|---|
| Text load (`ResetText`) ms | 39.6 |
| Time to first paint ms | 131.9 |
| Time to highlighting applied ms (classify on a worker, first window decorated) | 348.9 |
| Completion latency ms (worker thread, whole request) | 130.7 |
| Largest heartbeat gap ms | 132.0 |
| Gaps over 50 ms | 69 ms at start-up and 132 ms at 201 ms (window and Win2D start-up, before the text is on screen); none during the 50 scroll jumps, the highlighting or the completion |

Criterion "highlighting and completion work on 100,000 lines without freezing, UI stall < 100 ms": met after start-up; the single 132 ms gap is the first layout and paint of the window, not an editing or scrolling stall. Completion returns one item because the demo has no schema and the caret follows `FROM t` (keywords only); the latency is the cost of tokenizing, splitting and parsing, not of the number of items. Machine: the development PC, Release Native AOT, one run.

Not measured: typing latency on the 100,000-line text, memory, and the app's `UpdateDiagnostics` (the query tab runs `SqlDiagnosticsProvider` on the whole text at every change on the UI thread; it was not part of the demo and will stall on such scripts, a follow-up for the view-model).

## Missing LoomEditor APIs (requests, nothing changed in LoomEditor)

1. Registering an external TextMate grammar (`LanguageRegistry` is internal), so the four SQL grammars of `assets/grammars/` cannot be used; `SqlHighlighter` decorations replace them. A SQL grammar per dialect as built-ins, or a public `RegisterLanguage`, would give real tokenization, folding and bracket matching.
2. A "visible lines" API or event (the adapter derives it from `ScrollTop`, `LineHeight` and `ActualHeight`).
3. A cheap way to replace many decorations (an incremental `Append` costs O(total)); with it the whole file could be decorated.
4. Signature help and formatting provider interfaces; there is no `FormatDocumentCommand`, so formatting stays on `SqlFormatter` (not wired to the editor) and signature help is deferred.
5. A public text-changed event on the control (the adapter uses `Model.ContentChanged`).
