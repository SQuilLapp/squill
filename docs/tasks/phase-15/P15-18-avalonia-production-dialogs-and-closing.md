# P15-18 - Avalonia head: production confirmation dialog, transaction question and window closing

Status: done
Depends on: P15-16, P15-17

## Goal
Same as [P15-16](P15-16-winui-production-dialogs-and-closing.md) for the Avalonia head. Read P15-16 first: the dialog layout, the typed field, the checkbox rules, `Entrée` = `Annuler`, the three-button transaction question and the exit guard apply unchanged. Look at `Services/AvaloniaDialogService.cs`, `Views/UpdateAskWindow.cs` (a dialog window already built in code), `MainWindow.axaml.cs`.

## Design decisions (final)
- `ProductionConfirmWindow` and `TransactionAskWindow` in `Views/` (modal `Window`s centered on the main window, `SystemDecorations` per the other dialogs). Default and cancel button is `Annuler` / `Retour` (`IsDefault` and `IsCancel`); the confirm button has neither and is disabled until the typed text matches; `Entrée` inside the typed field must not confirm even when the text matches (handle `KeyDown` and mark it handled).
- Window closing: `MainWindow.Closing` with the cancel-then-close-again pattern of P15-16 (`e.Cancel = true`, await `PrepareExitAsync`, set the flag, `Close()`); **also** `IClassicDesktopStyleApplicationLifetime.ShutdownRequested` for the macOS application-menu `Quit` (and `Cmd+Q`), which does not go through `Window.Closing` of the main window on every platform: handle it the same way with `e.Cancel`. Check the macOS menu code added by the recent macOS menu-bar work before wiring it; a quit path that cannot be intercepted is listed in the report and the ADR.
- Headless tests where the pattern exists: the typed field gating the confirm button (wrong text disabled, matching text enabled, case-insensitive), `Entrée` cancels, locked checkbox, `Failed` hides `Valider`, result values returned by the service.

## Out of scope
- Anything WinUI.

## Tasks
- [x] Both windows, the service implementations (replacing the fallbacks), closing and shutdown hooks, headless tests.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` 0 warnings; tests green; Avalonia AOT publish with zero IL2xxx/IL3xxx.
- To check by hand (checklist of P15-19): the same steps as the WinUI dialogs on Windows; on Linux / macOS only what the owner can run is ticked.

## Suggested commit message
Add the production confirmation window, the transaction question and exit guard to the Avalonia head

## Report
- Done: `ProductionConfirmWindow` (M15 layout, typed field gating the confirm button through `ProductionGuard.Matches`, locked/checked option, confirm button neither default nor cancel, Enter and Escape cancel through a tunnelling `KeyDown` handler so a matching word never confirms) and `TransactionAskWindow` (`Valider` hidden on `Failed`, `Retour` default and cancel, environment stripe), wired into `AvaloniaDialogService`. `MainWindow.Closing` and `ShutdownRequested` (new `App` hook) cancel, ask through `Workspace.PrepareExitAsync`, then close again. 11 headless tests (dialogs and shutdown request).
- Quit paths: window close button and `desktop.ShutdownRequested` (macOS `Quit` / `Cmd+Q`, OS shutdown) are intercepted by code. NOT run on macOS or Linux: that the macOS application-menu `Quit` really raises `ShutdownRequested` is from the Avalonia contract, not observed.
- NOT looked at: any rendered dialog or comparison against M15 (no UI driving, the headless run does not write captures); light/dark; the figure in danger mono; the `Failed` variant on screen; closing the real window with an open transaction. Smoke only: the published AOT head starts and exits cleanly on a normal close.
- Deviations: no statement highlighter (plain mono), the confirm button is a plain `Button` with token colors (no `danger` fill style exists, hover/disabled use the Fluent states).
- Found in WinUI (P15-16, not changed): `ProductionConfirmDialog` takes the first five statements and prints `+ {Count-5} more` itself, but `ProductionRequestBuilder` already appends a localized `+ N more` line as the sixth item, so with more than five statements WinUI shows `+ 1 more` instead of the real count. The Avalonia window prints the sixth item as is.
- AOT publish: only IL2104 / IL3053 from third-party assemblies (`Microsoft.Data.SqlClient`, `System.Configuration.ConfigurationManager`), none from SQuilL code.
