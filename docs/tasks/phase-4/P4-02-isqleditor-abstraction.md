# P4-02 - Presentation: ISqlEditor and run target resolution

Status: done
Depends on: P4-01

## Goal
The editor abstraction required by ADR 0002 plus a headless stub.

## Design decisions (final)
- Namespace `Squill.Presentation.Editor`. `interface ISqlEditor { string Text { get; set; } int CaretOffset { get; set; } TextRange Selection { get; set; } event EventHandler? TextChanged; event EventHandler? CaretChanged; void SetDialect(DatabaseDialect d); void SetDiagnostics(IReadOnlyList<SqlDiagnostic> diagnostics); void SetCompletionProvider(ISqlCompletionProvider provider); void SetHoverProvider(Func<SqlRequest, HoverInfo?> provider); void FocusEditor(); }`.
- `StubSqlEditor : ISqlEditor` (plain in-memory; raises events; records the last diagnostics/providers) lives in `Squill.Presentation` because tests and a headless head use it.
- `static class RunTarget { IReadOnlyList<string> Resolve(ISqlEditor editor, DatabaseDialect d) }`: non-empty selection => statements split from the selected text; else the statement at the caret (`SqlStatementSplitter.GetStatementAt`); else empty list. `RunAll(ISqlEditor, d)` returns all statements of the text.

## Out of scope
- Concrete editors (P5-03, P5-09, P5-13).

## Tasks
- [x] Create the interface, stub and `RunTarget`.
- [x] Tests for `RunTarget` (selection spanning two statements, caret in the second, caret after the last `;`, empty text) and stub events.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add ISqlEditor abstraction, stub editor and run target resolution
