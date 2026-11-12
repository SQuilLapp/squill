# P15-16 - WinUI head: production confirmation dialog, transaction question and window closing

Status: done
Depends on: P15-12, P15-14, P15-15

## Goal
The WinUI head draws M15 for real, asks the three-way transaction question, and asks it again when the window is closed with an open transaction. Mockups `M15-Confirmation-clair-Windows.png` / `-sombre-` (the PNG wins). Recipe: section 2 (decisions 10 to 12), risk 12. Look at `Services/WinUiDialogService.cs`, `Views/ModalWindow.cs`, `Views/UpdateAskDialog.cs` (a dialog class already built in code), `MainWindow.xaml.cs` (no `Closing` handler today), `Views/Tokens.cs`.

## Design decisions (final)
- `ProductionConfirmDialog` (new, `Views/`, code-built like `UpdateAskDialog`, `ContentDialog` or `ModalWindow` following what `WinUiDialogService` uses for rich dialogs): width 560, red header band `danger` with white text `PRODUCTION · pg-prod-eu / ventes` and a warning icon; title 20 semibold; message with the figure in `danger` mono (the request gives `EstimatedRows`; if the message cannot be split reliably, style nothing and say so in the report); statement box (`bg` inset, 3 px `danger` left border, mono, first five statements, `+ N autres` muted); the typed field (label `Saisissez <word> pour confirmer` with the word in mono bold, placeholder = the word, mono) only when `RequiredText` is set; the checkbox when `ShowTransactionOption` (disabled when `TransactionOptionLocked`, checked by default); footer: hint `Entrée = Annuler` muted left, confirm button (danger fill, label from the request) and `Annuler` (outline, focus ring). **The confirm button is disabled until the typed text matches (`ProductionGuard.Matches`), is never the default button, and `Entrée` always cancels**; `Escape` cancels; initial focus on the typed field when there is one, else on `Annuler`. Highlighting of the statement: use a head highlighter only if one exists for a read-only box (check `Editors/`), otherwise plain mono (flag it).
- Implements `WinUiDialogService.ConfirmProductionAsync` (replacing the fallback of P15-12), returning `ProductionConfirmationResult(Confirmed, UseTransaction)`.
- `TransactionAskDialog` (`Views/`) implements `AskTransactionAsync`: title and message from the request, up to five statements in a mono list, buttons `Valider` (success, hidden when `State == Failed`), `Annuler les modifications` (danger outline), `Retour` (default and cancel, `Entrée` and `Escape` choose it); the environment color stripe of the dialog follows `Environment`.
- Window closing: `MainWindow` subscribes to `AppWindow.Closing`; when `Workspace.HasOpenTransaction`, set `args.Cancel = true`, await `Workspace.PrepareExitAsync`, and when it returns true set a `_closeConfirmed` flag and call `Close()` again (the second `Closing` passes straight through). The tunnels and connections are closed by the existing shutdown path; `ConnectionSession.DisposeAsync` rolls back (P15-08). The macOS-style menu `Quitter` / `Fichier > Quitter`, if present, goes through the same `Close()`.
- Dark theme and light theme both checked; all text through `Loc` / `UiText`; tokens only.

## Out of scope
- Avalonia (P15-18).

## Tasks
- [x] Both dialogs, the service implementations, the closing hook, strings if a head label is missing.
- [x] Capture the production dialog (both themes, with and without the typed field, locked checkbox) against M15 and the transaction question; anything not drivable goes to the report as "to check by hand".

## Acceptance criteria
- `rtk dotnet build Squill.Windows.slnx -c Release -warnaserror` 0 warnings; tests green; WinUI AOT publish with zero IL2xxx/IL3xxx.
- To check by hand: on a Production profile `DELETE FROM t;` shows the dialog, the red button stays disabled until the database name is typed, `Entrée` cancels, with the checkbox kept the status bar says `Transaction manuelle` / `Transaction ouverte`; closing the window with an open transaction asks and `Retour` keeps the application open.

## Suggested commit message
Add the production confirmation dialog, the transaction question and exit guard to the WinUI head

## Report
- Driven for real (AOT head, SQLite profile marked Production, French, light theme): `DELETE FROM clients;` shows M15 with the typed field `ventes.db`, the confirm button disabled until the word matches, `Entrée` cancels (also with the word typed); a `WHERE` update in manual mode shows the dialog without field and with the transaction checkbox checked and locked, focus on `Annuler`; confirming gives `Transaction ouverte ... 1 instruction`; closing the window asks `Quitter SQuilL ?`, `Retour` keeps the app open, `Annuler les modifications` rolls back and exits.
- NOT looked at: dark theme, the estimated-row figure in danger mono (SQLite gives no estimate, the splitting logic only styles the figure when it appears exactly once), the `+ N autres` line, the `Failed` variant (no `Valider`), the comparison with the M15 PNGs side by side, the environment stripe of the question for Staging/Development.
- Deviations: ContentDialog padding keeps the red band inset by 24 px instead of flush (M15 draws it flush); no read-only statement highlighter exists in `Editors/` for a plain box, so the statements are plain mono; `Quitter` has no menu item in the WinUI head, only the window close button exists.
