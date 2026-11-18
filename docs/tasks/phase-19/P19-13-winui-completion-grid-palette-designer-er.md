# P19-13 - WinUI: completion popup, grid filter, palette, designer, ER diagram and shared control states

Status: todo
Depends on: P19-07, P19-08

## Goal
The WinUI checklists `completion.md` and `grid-filter-palette.md` are mostly `agent: not driven`, and `visual-spec.md` section 12 records for WinUI: designer (M07) "dark only; row tags, draft state, issue badges not exercised", ER diagram (M08) "capture narrower than 1440 px, `Exporter` item and hops tooltip not checked", shared control styles "popups and pointer-over / pressed not driven, popup shadow is the WinUI default", palette and filter bar "not compared with the PNG" in dark. Recipe 3 of [debug-polish-recipe.md](debug-polish-recipe.md) applies.

## Design decisions (final)
- Head: WinUI AOT exe, SQLite scenario (`-Scenario Full`); no server needed (the PostgreSQL-only steps stay `not driven`).
- Steps, each in light and dark, French and English where the text differs:
  - `grid-filter-palette.md` rows 1-13 (filter bar, chips, column menu, search, read-only marker, pending edits block, export while filtered) and 14-27 (palette modes `>`, `#`, `!`, `:`, keys, focus return, narrow window); compare with `M05-Grille-*` and `M13-Palette-*` (the P18-28 note says WinUI light French was compared: re-check only what changed and the dark theme).
  - `completion.md`: the popup (Enter, Tab, Escape, Ctrl+Z, documentation panel, join row with `ON`, snippet tab stops) typed into the real editor. LoomEditor's popup is a control the app does not own: drive keys if the tooling can, else `agent: not driven`.
  - M07: open a table in the designer, add a column, rename one, create an issue (duplicate name, missing type) and read the row tags, the draft state and the issue badges; compare with `M07-Table-*`. M08: open the ER diagram, widen the window to 1440 px, use `Exporter` (PNG, SVG if offered) and hover a relation for the hops tooltip; compare with `M08-ER-*`.
  - Shared styles: pointer-over, pressed and focus on a button, a menu popup and a combo (move the mouse with `[System.Windows.Forms.Cursor]::Position` before `Capture-Window.ps1`; `agent: not driven` when the state cannot be reproduced). The popup shadow is recorded, not changed.
- Small fixes here; bigger ones proposed from `P19-17`. Update both checklists' `Result` / Note and the WinUI cells of `visual-spec.md` section 12.

## Out of scope
- Avalonia (P19-15), the features recorded as not built (row panel, `Commentaire`, Contraintes / Déclencheurs / DDL tabs of M07, PNG / PDF / Mermaid export if no such command exists).

## Tasks
- [ ] Drive, capture, read PNGs.
- [ ] Fixes, proposed cards, checklists and `visual-spec.md`.
- [ ] Close everything.

## Acceptance criteria
- Common acceptance (recipe 4) if code changed; report lists compared / not driven per area.

## Risks
- The largest matrix of the phase: if the session runs long, stop after the areas done, record the rest as `not driven (time)` and let a follow-up card continue; never stretch a claim.

## Suggested commit message
Compare the WinUI grid filter, palette, designer and ER screens in both themes
