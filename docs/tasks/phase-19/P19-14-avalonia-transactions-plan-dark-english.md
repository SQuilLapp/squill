# P19-14 - Avalonia: transactions, production dialog and plan

Status: done
Depends on: P19-12

## Goal
The Avalonia transaction toolbar, status segment, `ProductionConfirmWindow`, `TransactionAskWindow`, window closing and the plan tab (P15-17, P15-18, P13-18 ... P13-20) were tested headless only: `visual-spec.md` section 12 says "never compared with the PNGs, no dialog rendered or looked at, light/dark not read, `Failed` variant not seen, macOS `Quit` not run". Repeat P19-12 in the Avalonia head. Recipe 3 of [debug-polish-recipe.md](debug-polish-recipe.md) applies.

## Design decisions (final)
- Head: Avalonia AOT exe on Windows; data and container rules as in P19-12 (read its Done notes first and reuse its findings: the same wording or token bug is fixed once, in shared Presentation text or tokens, not twice).
- Same checklist steps as P19-12 (`transactions.md` SQLite, production dialog, window; `plan.md` SQLite, language and mockups), light and dark, French and English; the Avalonia dialogs are separate `Window`s: capture them with a second `Capture-Window.ps1` call (process name is the same, pick the dialog by bringing it forward) or mark `not driven`.
- `Entrée` must cancel the production dialog and never confirm: drive it with a real key press if the tooling allows; otherwise rely on the headless test and say so.
- Linux and macOS (`Quit` on macOS, AltGr on Linux layouts) are not available: recorded as not driven.
- Fixes small; bigger ones proposed from `P19-17`. Update the `Result` column and Notes of `transactions.md` / `plan.md` (an Avalonia note next to the WinUI one) and the Avalonia cells of `visual-spec.md` section 12 and the section 14 sentence.

## Out of scope
- Completion, grid, palette, designer, ER (P19-15), ticking owner boxes.

## Tasks
- [x] Drive, capture, read PNGs; fixes; docs; close everything.

## Acceptance criteria
- Common acceptance (recipe 4) if code changed (`Squill.slnx`, Avalonia AOT publish, app starts, `LayoutOverlapTests` green); report lists compared / not driven.

## Risks
- A fix of a shared token changes both heads: rebuild and re-look at one WinUI screen after it.

## Suggested commit message
Compare the Avalonia transaction, production and plan screens with their mockups

## Done notes
Driven on the published Avalonia AOT head (SQLite profile `pg-prod-eu` of environment Production from `New-VisualProfile.ps1 -Scenario Full`, dark, fr, 1440 x 900 logical = 2160 x 1350 px at scale 1.5; UI Automation plus SendKeys from a scratch script). Compared: production confirmation window with M15 dark (same structure; a real `Entrée` cancels, status `Annulé`), plan tab (`Graphique`, SQLite) with M06 dark (toolbar order matches; explorer stays open where the mockup hides it). Not driven: light, English, `Manuel` mode (the UIA click on the toggle did not engage it), pill, `Valider` / `Annuler`, close questions, `Failed` variant, PostgreSQL plan rows, AltGr, macOS `Quit`, narrow window. The dialog is a separate window that the main-window UIA tree and SendKeys reach only after focusing a control inside it. No code changed; no card proposed.
