# P19-15 - Avalonia: completion popup, grid filter, palette, designer and ER diagram

Status: partial (time)
Depends on: P19-13

## Goal
Avalonia was never looked at for M05, M13, M07 and M08 (`visual-spec.md` section 12: "headless render only, live window not driven", "capture narrower than 1440 px, `Exporter` item and hops tooltip not checked", palette and filter "compared in WinUI only"), and the Avalonia column of `completion.md` and `grid-filter-palette.md` is `agent: not driven`. Repeat P19-13 in the Avalonia head. Recipe 3 of [debug-polish-recipe.md](debug-polish-recipe.md) applies.

## Design decisions (final)
- Head: Avalonia AOT exe on Windows, SQLite scenario; read the Done notes of P19-13 first and reuse its findings and fixes (a difference found there that lives in shared code is already fixed; one in an Avalonia view is fixed here).
- Same areas as P19-13 (filter bar, column menu, search, read-only marker, export while filtered; palette modes and focus return; completion popup keys and documentation panel; M07 row tags, draft state, issue badges; M08 at 1440 px, `Exporter`, hops tooltip; shared control states), light and dark, French and English where it differs. The Avalonia editor is AvaloniaEdit (ADR 0002): its completion window is a different widget from the WinUI LoomEditor popup, so the `completion.md` rows are driven separately and any behavioral difference between the two popups goes under that file's Notes (it asks for it).
- Fixes small; bigger ones proposed from `P19-17`. Update the Avalonia boxes' `Result` text (never the ticks) and the Avalonia cells of `visual-spec.md` section 12.

## Out of scope
- Linux / macOS, features recorded as not built, ticking owner boxes.

## Tasks
- [x] Drive, capture, read PNGs; fixes; docs; close everything (partial, see Done notes).

## Acceptance criteria
- Common acceptance (recipe 4) if code changed (`Squill.slnx`, Avalonia AOT publish, app starts, `LayoutOverlapTests` green); report lists compared / not driven per area.

## Risks
- Same as P19-13: record what was not reached as `not driven (time)` rather than stretching.

## Suggested commit message
Compare the Avalonia grid filter, palette, designer and ER screens in both themes

## Done notes
- Driven on the Avalonia AOT head (dark, fr, SQLite `-Scenario Full`, 1440 x 900): grid filter (popup, chip, funnel, footer, chip removal, column menu), command palette (`Ctrl+K`, `comm`), completion popup after `select * from cl`, ER diagram (toolbar, six tables, relations, minimap, `Exporter` -> `SVG vectoriel`). Composites for M05, M13 and M08 read.
- Fixed: palette footer hints ran into the result count (same defect as WinUI in P18-28), `Controls/CommandPalette.cs`.
- Not driven (time): light theme, English, designer (M07), completion join / snippet rows, hops tooltip, pointer-over / pressed states, filter steps 3-4, 7, 9-14, palette steps 18-27. P19-13 stays partial; no card proposed.

