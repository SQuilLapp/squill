# P19-17 - WinUI home page: close the gaps found against M01

Status: todo
Depends on: P19-09

## Goal
P19-09 compared the WinUI home page with `M01-Accueil-*` (see the Note of `docs/verification/new-screens.md`). Fix what differs and is not a feature that is listed as not built.

## Design decisions (final)
- Cards (`src/Squill.App.WinUI/Views/HomePage.cs`): add the engine badge (two letters on the engine color, as in the explorer) and a second mono-free line `<engine> <version> . <last opened>` with the open-tab count on the right (`3 onglets`, `1 onglet`, `-` when none); the version is only shown when a live value exists, otherwise the engine name alone.
- Layout: center the content in a column of at most 1160 logical px.
- `DEMARRER`: add the subtitle of `Nouvelle connexion`; the fr/en catalog keys have parity. The tile `Ouvrir un fichier` (`Ctrl+O`) is NOT built: no such command exists in `src/` and phase 19 builds no feature. Record it as a known gap in the Note of `docs/verification/new-screens.md` (owner decision of 2026-11-18).
- Header search box: focus ring in the accent color while the box has the focus.
- Not in this card: `Importer des connexions`, `Console SQL ephemere`, projects, `.duckdb` / `.parquet` (`visual-spec.md` section 10), the window menu (`Requete`, `Base de donnees` are real features), update status text.

## Out of scope
- Avalonia (P19-11 compares it), any new feature.

## Tasks
- [ ] Presentation: open-tab count and last-opened text on `HomeViewModel` cards, with tests.
- [ ] WinUI: badge, second line, centered column, subtitle, focus ring; catalog keys fr/en.
- [ ] Capture and compare again (light, dark, fr, en); update `visual-spec.md` section 15 and the M01 row of section 12.

## Acceptance criteria
- Recipe 4 of `debug-polish-recipe.md` (`Squill.Windows.slnx`, AOT publish, app starts); the new capture read against the PNG.

## Suggested commit message
Bring the WinUI home page closer to its mockup
