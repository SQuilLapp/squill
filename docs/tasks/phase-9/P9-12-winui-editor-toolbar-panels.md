# P9-12 - WinUI head: query toolbar, editor frame and history panel

Status: todo
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
- [ ] `Views/QueryTabView.cs`: toolbar `Exécuter`, `Script`, `Arrêter`, separator, format and history icon buttons as P9-11, bound to the existing handlers.
- [ ] Editor frame: environment stripe, gutter/line numbers/current statement colors, diagnostics presentation as far as Loom supports it.
- [ ] Right panel `HistoryView`/`SnippetsView` restyled as section 5 with the existing fields; no pinned section without data.
- [ ] Query parameters panel as section 5.
- [ ] Tests for the testable helpers; state what needs the screenshot.

## Acceptance criteria
- `rtk bypass dotnet build Squill.Windows.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.Windows.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).
- `rtk dotnet publish src/Squill.App.WinUI -c Release -r win-x64` succeeds with zero IL2xxx/IL3xxx warnings.
- Visual check (`docs/design/visual-spec.md` section 11): the running app was captured in light and dark with `tools/visual/Capture-Window.ps1`, compared with `M04-Editeur-clair-Windows.png`, `M04-Editeur-sombre-Windows.png`, `M03-Workspace-*-Windows.png` (toolbar and editor) through `tools/visual/Compare-Mockup.ps1`, and every visible difference was fixed or listed in the card report with its reason. If a state could not be reached or driven, the report says which and what the owner must check by hand.
- No process left running (app, `dotnet build-server`, AOT executable).

## Suggested commit message
WinUI: query toolbar, editor frame and history panel
