# P15-15 - WinUI head: transaction toolbar group and status bar segment

Status: todo
Depends on: P15-10, P15-14, P9-12

## Goal
The WinUI query toolbar shows the `Auto-commit | Manuel` toggle, the pending pill, `Valider` and `Annuler`; the status bar shows the transaction segment. Mockups `M04-Editeur-clair/sombre-Windows.png` (toolbar and status bar, the PNG wins) and `M03-Workspace-*` (status text). Recipe: section 2 (decisions 2, 3, 6), section 3, risk 10. Look at how `Expliquer` / `Analyser` were added in P13-15: `Views/QueryTabView.cs` (code-built toolbar), `Views/StatusBar.cs`, `MainWindow.Chrome.cs`, `Views/Tokens.cs`, `Views/EditBarFormat.cs`.

## Design decisions (final)
- Toolbar group, right after `Analyser` (and its separator, as in M04): a segmented toggle `Auto-commit` | `Manuel` (the mockup truncates to `Man` only because the window is narrow; labels from `UiText`), bound to `Transaction.Mode` through `SetModeCommand`; **disabled with the tooltip `tx.unsupported`** when `!Transaction.IsSupported`; then the pill (`PendingCountText`, amber `warning-soft` / `warning`, hidden at count 0 and in `AutoCommit` with `State == None`), then `Valider` (success outline, check icon, `Ctrl+Alt+C` chip) and `Annuler` (danger outline, undo icon, `Ctrl+Alt+Z` chip) bound to `CommitCommand` / `RollbackCommand`. `Valider` / `Annuler` are visible when `Mode == Manual` or `State != None` (M03 shows them without the toggle's company, M04 with it: the toggle is always visible, the two buttons follow the rule), enabled by their commands. A `Failed` state turns the pill red (`danger-soft`) with the text `tx.status.failed` short form and disables `Valider`.
- Narrow windows: the chips hide first, then the pill text collapses to the count only, then `Valider` / `Annuler` lose their icon; **no overlapping text** (the recent narrow-window fix of the toolbar is the model, reuse its measure-based collapsing).
- Keyboard: `Ctrl+Alt+C` -> commit, `Ctrl+Alt+Z` -> rollback, only while a query tab is active and the editor does not consume them. **Check on the AZERTY layout and in LoomEditor / the plain editor that these chords do not type a character or clash with an existing accelerator**; if they do, stop with a report and a proposed alternative (recipe risk 10).
- Status bar: new segment after the connection text (M04): orange dot + `Transaction.StatusText` (`Transaction manuelle`, `Transaction ouverte depuis 4 min · 2 instructions`), red dot + text on `Failed`, hidden when the text is empty. It follows the active query tab's session and updates on `Transaction.PropertyChanged` (also `ElapsedText`). The segment is empty for non-query tabs.
- Tokens only (`SquillXxxBrush`); the amber and red soft backgrounds exist (`warning-soft`, `danger-soft`); a missing token is a stop-and-propose, never a hard-coded color. All text through `Loc` / `UiText`.
- Tooltips: toggle (`tx.tip.mode`), `Valider` (`tx.tip.commit`), `Annuler` (`tx.tip.rollback`), disabled reasons.

## Out of scope
- The dialogs and window closing (P15-16), Avalonia (P15-17).

## Tasks
- [ ] Toolbar group, narrow-window behavior, shortcuts, status bar segment, strings if a head-only label is missing.
- [ ] Capture (tools of P9-02, procedure of `visual-spec.md` section 11) of the toolbar and status bar with a Production profile in manual mode with two pending statements, light and dark, `SQUILL_LANGUAGE=fr`, against M04; whatever could not be driven (the head needs a live connection) goes to the report as "to check by hand".

## Acceptance criteria
- `rtk dotnet build Squill.Windows.slnx -c Release -warnaserror` 0 warnings; `rtk bypass dotnet test --solution Squill.Windows.slnx -c Release` green; WinUI AOT publish with zero IL2xxx/IL3xxx.
- To check by hand (also in `docs/verification/transactions.md`): toggle to `Manuel`, run an `INSERT`, the pill and the status text show `1 instruction`, `Annuler` empties it; narrow the window, nothing overlaps.

## Suggested commit message
Add the transaction toolbar group and status segment to the WinUI head
