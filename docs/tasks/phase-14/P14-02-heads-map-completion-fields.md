# P14-02 - Heads: map the new completion fields to LoomEditor and the plain editor

Status: done
Depends on: P14-01

## Goal
The popup of both heads shows and uses `FilterText`, `Documentation` and snippet items; the plain-text fallback editor never inserts raw snippet syntax.

## Design decisions (final)
- `src/Squill.App.Shared/LoomSqlProviders.cs` `Map`: `FilterText = item.FilterText`, `Documentation = item.Documentation is { } d ? new MarkdownString(d) : null` (same constructor the hover mapping already uses), `InsertTextRules = item.IsSnippet ? InsertTextRules.InsertAsSnippet : InsertTextRules.None`. Use only members present in LoomEditor 0.5.0 (they are, see the recipe). Nothing else in that file changes in this card. The file is linked into both heads: build both.
- `PlainSqlEditor.Accept` inserts `item.IsSnippet ? SnippetText.ToPlain(item.InsertText) : item.InsertText`; its list shows `Label` and `Detail` as today; its `RequestCompletions` filter uses `CompletionFilter` (already FilterText-aware after P14-01). It shows no documentation (known limit: interim fallback editor).
- The mapping is extracted to an `internal static` method testable without a UI: if `Map` needs `TextModel` (it does, for ranges) keep that part, and move the field mapping to a small `internal static void ApplyFields(LoomCompletionItem target, SqlCompletionItem source)` in the same file; the Avalonia headless tests (`tests/Squill.App.Avalonia.Tests`) call it. `LoomSqlProviders` is compiled under `#if SQUILL_LOOMEDITOR`; check that the Avalonia test project defines it (it does if `AvaloniaSqlEditorTests` compiles against it); if the symbol is not defined there, test through `AvaloniaSqlEditor.RequestCompletionAsync` instead and say so in the card report.

## Out of scope
- Any provider producing documentation or snippets (P14-15, P14-16); LoomEditor changes.

## Tasks
- [x] Test first (Avalonia head tests): an item with `FilterText`, `Documentation` and `IsSnippet = true` reaches the Loom item with the same values and the `InsertAsSnippet` rule; an item without them has `null` / `None`.
- [x] Implement in `LoomSqlProviders` and `PlainSqlEditor`.
- [x] WinUI head builds (`rtk dotnet build Squill.Windows.slnx -c Release -warnaserror`); by hand on WinUI is not needed yet (no provider uses the fields), P14-18 covers it.

## Acceptance criteria
- Recipe section 4 with both solutions built.

## Suggested commit message
Map completion filter text, documentation and snippets to the editors
