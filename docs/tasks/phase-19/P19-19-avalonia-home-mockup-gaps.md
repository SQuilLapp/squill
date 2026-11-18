# P19-19 - Avalonia home page: close the gaps found against M01

Status: todo
Depends on: P19-11

## Goal
P19-11 compared the Avalonia home page with `M01-Accueil-*` (Note of `docs/verification/new-screens.md`). Apply to Avalonia what [P19-17](P19-17-winui-home-mockup-gaps.md) decides for WinUI (engine badge, second line with open-tab count, `Ouvrir un fichier` tile and subtitle, header search focus ring; same exclusions), and fix the French tip text, which is clipped at the right edge of the tip card (`Placez le curseur dans une requete et appu...`): the text must wrap.

## Design decisions (final)
- Reuse the Presentation changes of P19-17 (open-tab count, last-opened text); the card therefore starts after P19-17 is done, or does that part itself if it is not.
- The column is already centered in Avalonia: leave it.

## Out of scope
- WinUI, any new feature.

## Tasks
- [ ] Avalonia: badge, second line, tile, focus ring, tip wrapping; catalog keys fr/en parity.
- [ ] Capture and compare again (light, dark, fr, en); update `visual-spec.md` section 15 and the M01 row of section 12.

## Acceptance criteria
- Recipe 4 of `debug-polish-recipe.md` (`Squill.slnx`, Avalonia AOT publish, app starts, `LayoutOverlapTests` green).

## Suggested commit message
Bring the Avalonia home page closer to its mockup
