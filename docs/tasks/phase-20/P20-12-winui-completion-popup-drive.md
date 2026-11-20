# P20-12 - WinUI: drive the completion popup, and find out why a single-entry popup stays open

Status: partial
Depends on: P20-01

## Goal
`docs/verification/completion.md` is `agent: not driven` for the popup in both heads (3 mentions), and P19-13 saw once, without investigating, that after `Ctrl+Enter` on `select * from clients` a popup with the single entry `clients` stayed open over the editor until `Escape`. Procedure and rules: recipe 3 of [verification-recipe.md](verification-recipe.md).

## Design decisions (final)
- Head: WinUI AOT exe, SQLite `-Scenario Full`. The popup is LoomEditor's (ADR 0002: a control the app does not own), fed by `src/Squill.Presentation/Completion` and the head glue (`grep -rn "Completion" src/Squill.App.WinUI`): read how the popup is shown and closed before driving.
- Drive the rows of the `completion.md` checklist (read it: popup on typing `select * from cl`, `Enter` / `Tab` accept, `Escape`, `Ctrl+Z` after an accept, documentation panel, join row with `ON`, snippet tab stops), light and dark, French and English where the text differs. Type with `Drive-Window.ps1 Type`, keys with `Key`.
- **Investigation (part of the card):** reproduce the stuck popup: type `select * from clients`, `Key Ctrl+Enter`, read a capture. If it reproduces, find who keeps it open (a completion request that finishes after the run, a hide event not raised when the editor loses the key focus to the run) in the head glue or the Presentation completion service. Fix test first: a Presentation-level test when the cause is in the request/close state machine (`tests/Squill.Presentation.Tests/Completion`), else a head helper test. If the cause is inside LoomEditor (a package the app does not own), do not patch it: record the exact behavior and report it for the owner, who maintains LoomEditor (`docs/adr/0002-editor-and-terminal-components.md`), as a proposed card or an upstream issue. If it does not reproduce in 5 attempts, record the attempts and stop that part.
- Anything else that differs is fixed if small, proposed if not.

## Out of scope
- Avalonia (P20-18: AvaloniaEdit's window is a different widget), changing completion ranking.

## Tasks
- [x] Drive (partial), capture, read PNGs; the investigation; fix with test or record; update `completion.md` Result cells and Notes (never ticks) and the WinUI cell of `visual-spec.md` where completion is mentioned.
- [x] Close everything.

## Acceptance criteria
- Recipe 2 common acceptance if code changed; Done notes say reproduced yes / no with the steps.

## Risks
- A popup is its own top-level window: it may not be in the main window's UIA tree (P19-14 found the same for dialogs); use `Focus-Dialog` or keys only and capture the screen region.

## Suggested commit message
Drive the WinUI completion popup and fix a popup that stays open after a run

## Done notes
- Status `partial`. Driven on the published WinUI head, dark, French: popup on `select * from cl`, join row with `ON`, Enter accept, one Ctrl+Z; results in `docs/verification/completion.md`. Not driven: Tab, Escape, snippets, keyword case, documentation panel (none seen on WinUI), light theme, English, other engines, 5,000 tables.
- Stuck popup reproduced (3 of 3): after typing `select * from clients` the popup keeps the single entry and `Ctrl+Enter` runs without closing it. Fixed in the head glue (`LoomSqlEditor.HideSuggest`, called by `QueryTabView` when the tab starts running), re-driven: closed. No automated test possible (control not instantiable in the head tests).
- Reported, not fixed: the popup is cut by the window's right edge when the caret is far right (LoomEditor placement); no footer hint line / documentation panel on WinUI.
- Build `Squill.Windows.slnx` 0 warnings; AOT publish of the head ran and started.
