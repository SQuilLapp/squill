# P15-18 - Avalonia head: production confirmation dialog, transaction question and window closing

Status: todo
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
- [ ] Both windows, the service implementations (replacing the fallbacks), closing and shutdown hooks, headless tests.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` 0 warnings; tests green; Avalonia AOT publish with zero IL2xxx/IL3xxx.
- To check by hand (checklist of P15-19): the same steps as the WinUI dialogs on Windows; on Linux / macOS only what the owner can run is ticked.

## Suggested commit message
Add the production confirmation window, the transaction question and exit guard to the Avalonia head
