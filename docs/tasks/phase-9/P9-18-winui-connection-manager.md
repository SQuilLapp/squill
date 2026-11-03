# P9-18 - WinUI head: connection manager dialog

Status: done
Depends on: P9-04, P9-06, P9-02

## Goal
The WinUI connection editor matches M02.

## Design decisions (final)
- Read `docs/design/visual-spec.md` sections 3 and 7 (final). `Views/ConnectionEditorDialog.cs` is built in code today: convert it to a secondary `Window` with an `.xaml` view and a code-behind (decision, same as the settings window of P9-24; it is modal-like: the main window is disabled while it is open).
- All content rules of P9-17 (engines, environments, folder combo, `Général`/`SSH` tabs, SSH card, footer, omitted elements).
- Keep `IDialogService` behavior (return value/cancellation) used by the Presentation layer unchanged.

## Out of scope
- Tags, SSL, proxy, driver tabs, generated URL, extra checkboxes (no data).

## Tasks
- [x] Rebuild the editor window as XAML.
- [x] Tests for the testable helpers (tile selection model, folder list), state what needs the screenshot.

## Acceptance criteria
- `rtk bypass dotnet build Squill.Windows.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.Windows.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).
- `rtk dotnet publish src/Squill.App.WinUI -c Release -r win-x64` succeeds with zero IL2xxx/IL3xxx warnings.
- Visual check (`docs/design/visual-spec.md` section 11): the running app was captured in light and dark with `tools/visual/Capture-Window.ps1`, compared with `M02-Connexions-clair-Windows.png`, `M02-Connexions-sombre-Windows.png` through `tools/visual/Compare-Mockup.ps1`, and every visible difference was fixed or listed in the card report with its reason. If a state could not be reached or driven, the report says which and what the owner must check by hand.
- No process left running (app, `dotnet build-server`, AOT executable).

## Suggested commit message
WinUI: connection manager dialog
