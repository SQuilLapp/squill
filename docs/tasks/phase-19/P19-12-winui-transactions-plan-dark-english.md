# P19-12 - WinUI: transactions, production dialog and plan in dark theme and English

Status: done
Depends on: P19-07, P19-08

## Goal
`docs/verification/transactions.md` and `plan.md` and `visual-spec.md` section 12 list what was never looked at in the WinUI head: the dark theme, the English text, the transaction pill with a count, `Annuler`, the `Failed` variant, the estimate figure and `+ N autres` of the production dialog, AltGr chords, and no side-by-side comparison with `M03`, `M04`, `M15`, `M06`. Recipe 3 of [debug-polish-recipe.md](debug-polish-recipe.md) applies.

## Design decisions (final)
- Head: WinUI AOT exe; data `New-VisualProfile.ps1 -Scenario Full` (SQLite profiles with `Production`, `Staging` environments: `pg-prod-eu` is Production). SQLite carries the transaction and plan structure flows; the `Failed` variant and the planner estimate figure need PostgreSQL: use the `-Keep` container as in P19-10 (same safety rules) only for rows that need it, and mark them `agent: not driven (no server)` if it cannot start.
- Steps (read each checklist for the exact text; drive, capture, record):
  - `transactions.md`: SQLite section, "Production dialog" section (compare with `M15-Confirmation-clair/sombre`), "Window and presentation" section; plus the PostgreSQL `Failed` step.
  - `plan.md`: SQLite section, the PostgreSQL steps that fit one container, "Language and mockups" section (compare with `M06-Plan-clair/sombre`, `Arbre`, `Graphique`, `Texte`).
  - Each in light and dark, French and English. Compare M03 / M04 toolbar and status bar (pill with count, `Valider`, `Annuler`) with the captures.
- AltGr chords: `Ctrl+Alt+C` / `Ctrl+Alt+Z` on a layout with AltGr (French AZERTY): drive with `SendKeys`-like automation only if the capture tooling allows real key events; otherwise `agent: not driven (no keyboard automation)`.
- Fix small differences (token, spacing, wording, a missing English key) here; bigger ones proposed `P19-17` onwards. Update the status rows of `visual-spec.md` section 12 and the section 14 "What was compared" sentence.

## Out of scope
- Avalonia (P19-14), MySQL / SQL Server rows, ticking owner boxes.

## Tasks
- [x] Drive and capture the matrix; read every PNG.
- [x] Fixes and proposed cards; `Result` column and Notes of the two checklists; `visual-spec.md`.
- [x] Close the app and any container.

## Acceptance criteria
- Common acceptance (recipe 4) if code changed (`Squill.Windows.slnx`, WinUI AOT publish, app starts); report lists compared (theme, language) and not driven.

## Risks
- A dialog opened by the app is a separate top-level window: `Capture-Window.ps1` takes the main window only. Capture such dialogs through a second invocation after bringing them to the front, or mark `not driven`.

## Suggested commit message
Compare the WinUI transaction, production and plan screens in dark theme and English

## Done notes
Driven on the published AOT WinUI head (SQLite profiles from `New-VisualProfile.ps1 -Scenario Full`, UI Automation plus SendKeys from a scratch script) at 1440 x 900 logical (2160 x 1350 px on this 150 % display; `Capture-Window.ps1` run from a non-DPI-aware shell reports scale 1 and gives a 960 px wide layout, so pass `-Width 2160 -Height 1350` here). Compared: transaction toolbar group and status text (dark fr, light en), production dialog (dark fr with M15 dark, light en). Not driven: PostgreSQL rows (`Failed` variant, planner figure, no server started), AltGr chords, close questions, narrow window, plan tab (`plan.md` language rows), MySQL / SQL Server. Differences are in P19-20 (light theme with a dark system theme leaves the workspace dark; dialog frame; keycaps). No code changed.
