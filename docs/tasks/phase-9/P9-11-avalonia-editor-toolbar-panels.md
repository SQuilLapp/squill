# P9-11 - Avalonia head: query toolbar, editor frame and history panel

Status: done
Depends on: P9-08, P9-02

## Goal
The query tab chrome around the editor matches M04 and M03.

## Design decisions (final)
- Read `docs/design/visual-spec.md` sections 3 and 5 (final).
- Environment stripe: 3 px, `SquillEnv*Brush` of the tab's connection, absent for `None`.
- Completion popup and signature help are drawn by the editor control; style them through the control's theme resources where the editor exposes them (AvaloniaEdit completion window styles), using tokens. If a part cannot be styled without reflection or patching the package, list it as not matched.

## Out of scope
- The plan view, auto-commit/commit/rollback controls (no feature).

## Tasks
- [x] `QueryTabView.axaml`: a toolbar above the editor with `Exécuter` (`RunCommand`, `Ctrl+Entrée` chip), `Script` (`RunAllCommand`, `Ctrl+Maj+Entrée` chip), `Arrêter` (`CancelCommand`, disabled while not running), a separator, and at the right the format icon button (`FormatDocumentCommand`) and the history icon button (toggles the right history panel).
- [x] Editor frame: gutter, line-number colors, current-statement background (`SquillSelectionBrush`) and margin triangle if the editor exposes the run target highlight (P4-02/P5-05; otherwise only the background), diagnostics: wavy underline in `SquillDangerBrush` and the inline message pill after the line end if the editor supports inline adornments (else list as not matched).
- [x] Right panel `Historique` / `Snippets` (existing `HistoryView`/`SnippetsView`): underline tabs, search box, entry layout of section 5 using the history entry fields that exist (time, duration, rows, error flag), `SNIPPETS ÉPINGLÉS` only if the snippet store has a pinned notion (else omit).
- [x] Query parameters panel (the existing parameter prompt) under the editor as section 5: rows, type dropdown, value field, `Transmettre NULL`, `Valeur requise`, `Ignorer` and `Exécuter avec ces valeurs` buttons; strings from the catalog.
- [x] Headless tests: toolbar commands bound, cancel disabled when idle, parameter row validation message, history entry formatting.

## Acceptance criteria
- `rtk bypass dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).
- `rtk dotnet publish src/Squill.App.Avalonia -c Release -r win-x64` succeeds with zero IL2xxx/IL3xxx warnings.
- Visual check (`docs/design/visual-spec.md` section 11): the running app was captured in light and dark with `tools/visual/Capture-Window.ps1`, compared with `M04-Editeur-clair-Windows.png`, `M04-Editeur-sombre-Windows.png`, `M03-Workspace-*-Windows.png` (toolbar and editor) through `tools/visual/Compare-Mockup.ps1`, and every visible difference was fixed or listed in the card report with its reason. If a state could not be reached or driven, the report says which and what the owner must check by hand.
- No process left running (app, `dotnet build-server`, AOT executable).

## Suggested commit message
Avalonia: query toolbar, editor frame and history panel

## Report
Done in `QueryTabView`, `HistoryView`, `SnippetsView`, `AvaloniaSqlEditor`, `SqlColorizer`, `AvaloniaDialogService`; tests in `EditorToolbarTests`. Captured light and dark against M04 (dark side by side read; light checked on the capture alone). Deviations, to be checked by the owner:
- Not matched: wavy diagnostic underline (AvaloniaEdit decorations have no wavy style; a solid underline in `SquillDangerBrush`/`SquillWarningBrush` is drawn), inline diagnostic pill, margin triangle and current-*statement* background (the current *line* gets `SquillSelectionBrush`), completion popup and signature help styling, `Expliquer`, auto-commit/commit controls (no feature).
- The history/snippets panel is a right panel opened by the toolbar history button; the activity rail history and snippets buttons still open the existing left panels. `SNIPPETS ÉPINGLÉS` omitted (no pinned notion).
- The parameters prompt stays a modal window (the view-model awaits `IDialogService`), restyled with the catalog strings, `Valeur requise` validation and `Ignorer`/`Exécuter avec ces valeurs`; the type dropdown is omitted (parameters carry no type) and the panel is not drawn under the editor.
- The environment stripe was only checked by tests: the sample profile has no environment.
- AOT publish: no IL2xxx/IL3xxx from SQuilL code; only the existing IL2104/IL3053 warnings of the Microsoft.Data.SqlClient and System.Configuration.ConfigurationManager packages.
