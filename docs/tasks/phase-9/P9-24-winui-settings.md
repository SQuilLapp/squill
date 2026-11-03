# P9-24 - WinUI head: settings window

Status: done
Depends on: P9-06, P9-03, P9-02

## Goal
The WinUI settings window matches M14 for the settings that exist.

## Design decisions (final)
- Same content rules as P9-23. `Views/SettingsDialog.cs` (code-built `ContentDialog`) becomes a secondary `Window` with an `.xaml` page, opened modal-like (main window disabled while open) from the same entry points; `IDialogService`/settings view-model behavior unchanged.

## Out of scope
- The unsupported settings listed in P9-23.

## Tasks
- [x] Rebuild the settings window as XAML.
- [x] Tests for the testable helpers; state what needs the screenshot.

## Acceptance criteria
- `rtk bypass dotnet build Squill.Windows.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.Windows.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).
- `rtk dotnet publish src/Squill.App.WinUI -c Release -r win-x64` succeeds with zero IL2xxx/IL3xxx warnings.
- Visual check (`docs/design/visual-spec.md` section 11): the running app was captured in light and dark with `tools/visual/Capture-Window.ps1`, compared with `M14-Parametres-clair-Windows.png`, `M14-Parametres-sombre-Windows.png` through `tools/visual/Compare-Mockup.ps1`, and every visible difference was fixed or listed in the card report with its reason. If a state could not be reached or driven, the report says which and what the owner must check by hand.
- No process left running (app, `dotnet build-server`, AOT executable).

## Suggested commit message
WinUI: settings window
