# ADR 0002 - Editor and terminal components (LoomEditor, LoomTerminal)

- Status: accepted (direction), details pending LoomEditor release
- Date: 2026-10-29

## Context
SQuilL needs a SQL editor (the riskiest component, R1) and, where useful, a terminal (e.g. an SSH or local shell tab, `psql`/`mysql` consoles). The project owner maintains two native, Native-AOT-compatible components, both MIT licensed, no WebView:

- **LoomEditor**: a C# port of Monaco for **WinUI 3 only** (Win2D rendering). Not published yet. Per its roadmap docs (its README is stale), it is at phase 11 (inline comments): text model, rendering, editing, TextMate + themes, editor features, accessibility, an LSP client (JSON-RPC over stdio: diagnostics, hover, completion, go-to-definition) and a diff editor exist. Phase 8's manual test plan is still pending, and the LSP client has not been verified against a real server for completion. Phase 12 (Avalonia backend) is planned, not scheduled.
- **LoomTerminal**: a C# port of xterm.js, with WinUI 3 and Avalonia backends and a headless core. `0.1.0-alpha`, not published yet.

## Decision
- The SQL editor on Windows is **LoomEditor**, consumed through a thin `ISqlEditor` abstraction owned by `Squill.Presentation`, so view-models never reference a control type.
- Terminal tabs use **LoomTerminal** (WinUI 3 on Windows, Avalonia on macOS/Linux).
- Both are consumed as NuGet packages once published; until then, as project references to a local checkout (`D:\LoomEditor`, `D:\LoomTerminal`), kept out of the committed solution.
- SQL-specific intelligence (dialect tokenization, completion, signature help, diagnostics, formatting) stays in `Squill.Sql` and is exposed to the editor through LoomEditor's extension points (decorations, completion providers; LSP-shaped contracts if phase 8 lands first).

## Settled points (owner, 2026-10-29)
1. **macOS/Linux editor.** LoomEditor will get an Avalonia backend (its phase 12, not scheduled). Until then the Avalonia heads use AvaloniaEdit behind `ISqlEditor`, then switch.
2. **SQL highlighting.** LoomEditor provides the TextMate engine, but its 9 built-in grammars (C#, CSS, HTML, JavaScript, JSON, Markdown, Python, TypeScript, XML, YAML) include **no SQL**. A SQL grammar per dialect (PostgreSQL, MySQL/MariaDB, T-SQL, SQLite) must be added, either to LoomEditor's built-ins (owner) or registered by SQuilL if the engine accepts external grammars (to verify). `Squill.Sql` also ships a highlighter for the AvaloniaEdit interim.
3. **Completion.** The popup is in place (verified in code, not run): fuzzy filter, auto and trigger-character activation, commit characters, additional edits, snippet items, WinUI `SuggestWidget`. SQuilL writes an `ICompletionItemProvider` (tables, columns, keywords, aliases) in `Squill.Sql` and registers it in `LanguageFeatureRegistry`; hover and go-to-definition use `IHoverProvider` and `IDefinitionProvider`. No stdio server process is needed. Contracts stay LSP-shaped so AvaloniaEdit can reuse them.
4. **Gaps in LoomEditor's LSP phase** (Core has no signature-help or formatting provider interface; snippet completion items exist): SQuilL needs signature help and formatting, so these must be requested from LoomEditor or built on SQuilL's side.
5. **Versions.** LoomEditor, LoomTerminal and SQuilL all move to .NET 11 RC 3.

## Still open
- Run-under-cursor and query parameters are SQuilL UI features built on the editor's selection/caret API; confirm nothing extra is needed from LoomEditor.
- Signature help and formatting provider interfaces in LoomEditor (absent today); external grammar registration.

- P5-13 result (details and numbers in `docs/poc/editor.md`): completion, hover and markers work through LoomEditor's public providers; external grammar registration is not public, so SQL highlighting uses `SqlHighlighter` decorations of the viewport; signature help is deferred and formatting is not wired (no provider interface or `FormatDocumentCommand` in LoomEditor).
- P6-02 result: both heads show LoomTerminal tabs through one `LoomTerminalHost` (`src/Squill.App.Shared`, linked into each head) behind `SQUILL_LOOMTERMINAL`, enabled by `LoomTerminalPath` in the git-ignored `Directory.Build.local.props`; without it no terminal UI is compiled.

## Consequences
- R1 changes from "build an editor" to "integrate and extend LoomEditor", gated by its phases 4-5; a SQuilL-side spike (plain-text editor in a WinUI shell with a stub `ISqlEditor`) can start now.
- Interface-first: `ISqlEditor` lets the WinUI head swap implementations and lets tests run headless.
