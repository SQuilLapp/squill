# P18-28 - Verification: M05 and M13 captures and the phase 17 checklist

Status: done
Depends on: none

## Goal
Close what P17-17 left open: nobody ever looked at the phase 17 grid filter and command palette in a window or compared them with `M05-Grille-*` and `M13-Palette-*` (`docs/verification/grid-filter-palette.md`, "Not driven"). Do what an agent can do and leave the rest to the owner, honestly marked.

## Design decisions (final)
- Procedure of `visual-spec.md` section 11 with `tools/visual/` (`New-VisualProfile.ps1`, `Capture-Window.ps1`, `Compare-Mockup.ps1`): WinUI AOT head first, then Avalonia, light and dark, French. Data: the seed of `tools/visual/seed.sql` (a SQLite profile; the Production / Staging profiles for the palette are extra SQLite profiles with those environments, so no server is needed).
- Steps to drive and capture: M05 with a filter chip, the column menu open, a sorted header, the search box with text, the footer, `filtre actif`; M13 with nothing typed, `comm`, each prefix mode, a highlighted match. Drive through UI automation where the capture tool allows (keys `Ctrl+K`, `Ctrl+F`); what cannot be driven is listed.
- Fix **small** visible differences found (spacing, wording, a missing token) in this card; a bigger one becomes a proposed card `P18-33` onwards (added to the index by the report, not invented here).
- Update the checklist `docs/verification/grid-filter-palette.md`: for each step an agent drove, put `agent: ok` / `agent: bug -> <card>` / `agent: not driven (why)` in the `Result` column and leave the WinUI / Avalonia boxes **unticked** (only the owner ticks them); fill the Note with the differences. Update the M05 / M13 rows of `visual-spec.md` section 12 with what was compared.

## Out of scope
- Ticking the owner's boxes, redesigning anything.

## Tasks
- [x] Captures (kept out of the repository except the ones the checklist links, small PNGs under `docs/verification/captures/` only if the repo already does that; otherwise paths in the report).
- [x] Small fixes, checklist and spec rows updated.

## Acceptance criteria
- Common acceptance (recipe 7) if code changed; every process started is closed; the report lists what was compared in which theme and head, and what was not.

## Suggested commit message
Verify the grid filter and command palette against the mockups
