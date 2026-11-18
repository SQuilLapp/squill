# P19-18 - WinUI: close the sessions and privileges gaps found against M11 and M12

Status: todo
Depends on: P19-10

## Goal
P19-10 compared the WinUI sessions (M12) and privileges (M11) screens on PostgreSQL with their mockups (Note of `docs/verification/new-screens.md`). Fix what differs and is not listed as not built in `visual-spec.md` section 10.

## Design decisions (final)
- First reproduce each item at 1440 x 900 logical px with `tools/visual/` and a PostgreSQL container (recipe 3); an item that cannot be reproduced is recorded, not changed.
- M11 matrix: the mockup has 7 privilege columns (`SELECT`, `INSERT`, `UPDATE`, `DELETE`, `TRUNCATE`, `REFERENCES`, `TRIGGER`); the capture shows only `SELECT` and `INSERT`. Find out whether the other columns are clipped or missing (check the privilege kinds the reader returns and the matrix width), then fix; add the hint line under the matrix and stop the legend (`A revoquer`) being cut off.
- M12 table: columns after `Duree` (`Attente`, `Requete`) must be reachable without horizontal scrolling at 1440 px (narrower columns or a flexible `Requete` column).
- Plural: `1 membres` becomes `1 membre` (catalog keys with a singular form, fr and en, parity test).
- The `Terminer la session` confirmation on Dev uses the system dark dialog with `OK` / `Annuler` inside a light theme: use the same themed dialog as Production, or at least the app theme.

## Out of scope
- Tabs `General` / `Appartenances` / `Privileges par defaut`, `Dupliquer`, `Supprimer le role...`, `Effet sur les membres` (not built), Avalonia (P19-11).

## Tasks
- [ ] Tests first where a pattern exists (catalog plural, matrix columns), then the fixes.
- [ ] Capture and compare again (light, dark, fr, en); update `visual-spec.md` sections 17 and 18.

## Acceptance criteria
- Recipe 4 of `debug-polish-recipe.md` (`Squill.Windows.slnx`, AOT publish, app starts); the new capture read against the PNG; container removed.

## Suggested commit message
Bring the WinUI sessions and privileges screens closer to their mockups
