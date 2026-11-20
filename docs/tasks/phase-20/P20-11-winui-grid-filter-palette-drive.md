# P20-11 - WinUI: finish the grid filter and command palette checklists (dark, English, keys)

Status: partial
Depends on: P20-01, P20-02

## Goal
P19-13 compared only one filter chip and footer in dark French. `docs/verification/grid-filter-palette.md` still has `agent: not driven` WinUI results for grid steps 3-14 and palette steps 15-27 (26 `not driven` mentions); the palette in dark was never opened because synthetic keys did not reach it (P20-01 fixes that). Procedure and rules: recipe 3 of [verification-recipe.md](verification-recipe.md).

## Design decisions (final)
- Head: WinUI AOT exe, SQLite `-Scenario Full` (`select * from clients` and the other tables of `seed.sql`), French light and dark, English light for steps whose text differs; PostgreSQL-only wording stays `not driven (no server)` unless the P20-03 container is up (then use it for the cut-notice free steps only).
- Steps, in order, each driven with `Drive-Window.ps1` and read in a capture: grid 3 (text `contient` with an accent and other case, `est NULL`, a date), 4 (column menu sort), 5 (value list, counts, NULL row, search, apply), 6 (remove chip, `Effacer les filtres`), 7 (`Ctrl+F`, `Esc`), 8 (read-only marker), 9 (pending edit blocks the bar), 10 (export CSV while filtered: file holds every loaded row; read the file), 13 (second query does not inherit filters), 14 (compare with `M05-Grille-clair-Windows.png` / `-sombre-`); palette 15 (`Ctrl+K` from the editor, the grid and the explorer), 16-18 (sections, pills, prefixes `>` `#` `!` `:`), 19 (Up / Down wrap, `Enter`, `Ctrl+Entrée`), 20 (focus returns), 21-22 (`!` insertion, `:3`), 24, 25 (window about 600 px wide), 27 (compare with `M13-Palette-*` light and dark). Steps 11 and 12 (slow query, 100,000 rows) are driven only if a script can be run (`select` over a recursive CTE for 100,000 rows) and the window stays draggable; else `not driven`. Step 23 needs two open connections (Production and Staging profiles of `-Scenario Full`). Step 26 (Narrator) stays `not driven (no screen reader)`.
- Small fixes in the card with tests where a pattern exists (`tests/Squill.App.WinUI.Tests`, `tests/Squill.Presentation.Tests/Palette`); bigger ones proposed.

## Out of scope
- Avalonia (P20-18), completion popup (P20-12), the features recorded as not built (ADR 0014).

## Tasks
- [ ] Drive and capture (read every PNG); fix; update the `Result` / WinUI Note of `grid-filter-palette.md` and the WinUI cells of `visual-spec.md` section 12 rows M05 and M13.
- [ ] Close everything.

## Acceptance criteria
- Recipe 2 common acceptance if code changed; report lists per step `ok` / `bug` / `not driven (why)`.

## Risks
- The palette steps 19-22 depend on focus: if `Key` exits 2 (cannot get focus) retry once after a click on the editor, else `not driven (focus)`; do not fall back to guessing.

## Suggested commit message
Drive the WinUI grid filter and command palette checklists in both themes and English

## Done notes
- Status `partial` (context stop rule). Driven on the published WinUI head, dark, French; results per step in `docs/verification/grid-filter-palette.md`: steps 4, 5, 7, 9, 10, 13 ok; 3, 6, 15, 16, 18 partial; 8 fixed; 14 and 27 in the Note; 11, 12, 19-25 `not driven (time)`, 26 no screen reader. Not driven at all: English, light theme.
- Fixes (small, no new test pattern existed for them; verified in the republished window): the read-only marker under a filter (`QueryTabView.ShowEditBar`), the export menu title count (loaded rows, not filtered), the CSV separator label (`,`, as the exporter writes). Avalonia has the same marker and title count code: left to P20-18.
- Tooling: the native save dialog (`PickerHost`) is not reachable by `Drive-Window.ps1` (`Focus-Dialog` finds no secondary window and the foreground rule refuses keys); it was driven with a one-off mouse click and `SendKeys` after checking the foreground process. A card could add a `-Process PickerHost` mode.
- Build 0 warnings (both solutions), tests green (4227 passed, 342 skipped), `check-aot-warnings.ps1` OK.
