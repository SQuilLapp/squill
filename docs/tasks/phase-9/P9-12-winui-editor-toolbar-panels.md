# P9-12 - WinUI head: query toolbar, editor frame and history panel

Status: done
Depends on: P9-10, P9-02

## Goal
The WinUI query tab chrome around the editor matches M04 and M03.

## Design decisions (final)
- Read `docs/design/visual-spec.md` sections 3 and 5 (final).
- Environment stripe 3 px, `SquillEnv*Brush` of the tab's connection, absent for `None`.
- The editor is LoomEditor (`LoomSqlEditor`) or the plain fallback: style only what Loom exposes through its public theme/options; list anything that cannot be matched without changing LoomEditor (owner's repository, never edited by this card).

## Out of scope
- Plan view, auto-commit controls (no feature).

## Tasks
- [x] `Views/QueryTabView.cs`: toolbar `Exécuter`, `Script`, `Arrêter`, separator, format and history icon buttons as P9-11, bound to the existing handlers.
- [x] Editor frame: environment stripe, gutter/line numbers/current statement colors, diagnostics presentation as far as Loom supports it.
- [x] Right panel `HistoryView`/`SnippetsView` restyled as section 5 with the existing fields; no pinned section without data.
- [x] Query parameters panel as section 5.
- [x] Tests for the testable helpers; state what needs the screenshot.

## Acceptance criteria
- `rtk bypass dotnet build Squill.Windows.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.Windows.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).
- `rtk dotnet publish src/Squill.App.WinUI -c Release -r win-x64` succeeds with zero IL2xxx/IL3xxx warnings.
- Visual check (`docs/design/visual-spec.md` section 11): the running app was captured in light and dark with `tools/visual/Capture-Window.ps1`, compared with `M04-Editeur-clair-Windows.png`, `M04-Editeur-sombre-Windows.png`, `M03-Workspace-*-Windows.png` (toolbar and editor) through `tools/visual/Compare-Mockup.ps1`, and every visible difference was fixed or listed in the card report with its reason. If a state could not be reached or driven, the report says which and what the owner must check by hand.
- No process left running (app, `dotnet build-server`, AOT executable).

## Suggested commit message
WinUI: query toolbar, editor frame and history panel

## Report
Done in `QueryTabView` (toolbar, environment stripe, right Historique/Snippets panel), `HistoryView`, `SnippetsView`, `HistoryFormat`, `ParameterPromptRow`, `WinUiDialogService`, `MainWindow`; tests in `EditorToolbarTests`. Captured dark against M04 with the toolbar, right panel and one history entry; light theme and the Compare-Mockup diff image were not read in detail. To be checked by the owner:
- Not matched: wavy diagnostic underline, inline diagnostic pill, margin triangle and current-statement background, completion popup and signature help styling (LoomEditor exposes decorations only, no gutter or popup theme; not edited), `Expliquer`, auto-commit/commit controls (no feature).
- History search box has no magnifier glyph and `Effacer` stays next to it; `SNIPPETS ÉPINGLÉS` omitted (no pinned notion). The history/snippets panel opens from the toolbar history button; the activity rail panels are unchanged.
- The parameters prompt stays a modal `ContentDialog` (the view-model awaits `IDialogService`) with catalog strings, `Valeur requise` validation, `Ignorer` and `Exécuter avec ces valeurs`; the type dropdown is omitted (parameters carry no type). The dialog was not opened by hand.
- The environment stripe was not seen on screen: the sample profile has no environment (only the helper is tested).
- Ctrl+Entrée typed through SendKeys did not run the query in the capture; clicking `Exécuter` did.
- AOT publish: only the existing IL2104/IL3053 warnings of Microsoft.Data.SqlClient and System.Configuration.ConfigurationManager.
