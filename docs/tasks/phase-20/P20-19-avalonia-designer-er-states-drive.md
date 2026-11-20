# P20-19 - Avalonia: drive the table designer (M07), the ER diagram (M08) and pointer states

Status: partial
Depends on: P20-13

## Goal
P19-15 drove the Avalonia ER diagram in dark French (toolbar, six tables, relations, minimap, `Exporter` -> `SVG vectoriel`) but not the designer (M07), the hops tooltip, the light theme, English, or pointer-over / pressed states. Repeat P20-13 on Avalonia. Procedure and rules: recipe 3 of [verification-recipe.md](verification-recipe.md).

## Design decisions (final)
- Head: Avalonia AOT exe, SQLite `-Scenario Full`, light and dark, French; English only where a label differs; scale 1.5 pixel sizes as in P20-16.
- Read the Done notes of P20-13 first and reuse its findings and fixes. Same areas: M07 (add / rename column, duplicate-name and missing-type issues, row tags, draft state, issue badges, `Appliquer`), M08 (1440 x 900 logical, the `Exporter` item list, hops tooltip by `Hover` over a relation), shared states (pointer-over and focus on a button, a menu and a combo; pressed as in P20-13).
- Compare with `M07-Table-*` and `M08-ER-*`; small fixes with tests (`tests/Squill.App.Avalonia.Tests/DesignerUiTests.cs`, `DiagramUiTests.cs`); propose bigger ones.

## Out of scope
- WinUI, features recorded as not built, Linux / macOS.

## Tasks
- [x] Drive, capture, read PNGs; fixes; update the Avalonia cells of `visual-spec.md` section 12 (rows M07, M08, shared styles) with the card id.
- [x] Close everything.

## Acceptance criteria
- Recipe 2 common acceptance if code changed; report lists compared / not driven per area.

## Risks
- Designer apply changes the scenario file: scratch `-Dir` per pass. The ER canvas is custom drawn: coordinates from a capture.

## Suggested commit message
Drive the Avalonia designer, ER diagram and control states against their mockups

## Done notes
- Status `partial`. Driven on the published Avalonia AOT head (HEAD `4fef415` code, no code changed), SQLite `-Scenario Full` in a scratch dir, French, **dark and light**, window 1.5 scale (2404 x 1615 px captures); every capture was read as a PNG. English was not driven.
- **M07 designer:** opened with the explorer row, then `Ouvrir l'editeur de table` of the properties panel (the explorer context menu popup does not appear in captures, see below). `Ajouter une colonne` gave a `NOUVELLE` row (green tint and tag), the draft chip `1 modification en brouillon`, a live DDL preview and two `Avertissement` badges (dark and light). Renaming the new column to `id` gave the `Erreur` badge `Le nom de colonne 'id' est utilise deux fois`, a red outline on both rows (it stops before the FK column), `Appliquer` greyed out and an empty DDL preview; clearing the type added `La colonne 'id' n'a pas de type de donnees`; focus rings on the type and default boxes are fine. Name cell text stays readable (the WinUI P20-22 problem does not exist here). Not driven: `Appliquer` itself, `MODIFIEE` row tag, English.
- **Bug found (not fixed, proposed as P20-23):** a freshly opened designer on an untouched table (`clients`, `commandes`) already shows the full SQLite rebuild script, an enabled `Appliquer`, a dot on the tab and the bogus warning `Les NULL existants empechent 'id' de devenir non nullable`; `Appliquer` also stays enabled after `Annuler les modifications`. See [P20-23](P20-23-designer-sqlite-fresh-open-reports-changes.md).
- Differences from M07 not fixed (known gaps): no `Commentaire` column, no estimated impact card, no Contraintes / Declencheurs / DDL tabs, no `Executer dans une transaction` options.
- **M08 ER diagram (dark and light):** rail button opens `ER - local-sqlite`: toolbar `Mise en page auto`, layout buttons `1 2 3`, zoom `100 %`, `Ajuster Ctrl+0`, `Types`, `Vues`, `Focaliser`, `Tout afficher`, `Exporter`; table list `TABLES 6 / 7` with a filter; six tables with PK / FK tags, crow's feet, a dashed optional relation, minimap, status hint. The hops tooltip appeared by `Hover` over the `commandes.client_id` relation (name bold, `main.commandes.client_id n - 1 main.clients.id`), readable in both themes; the ON DELETE / ON UPDATE rule line of M08 is missing and the hovered relation is not drawn in accent. Differences from the mockup (segmented `En couches / Orthogonal / Libre`, `Commentaires`, `Cles seulement si replie`, 24-table panel with checkboxes, `Glissez une table...` hint) are known design gaps, not new. At 100 % the tables at the right are cut by the canvas (no fit on open).
- **Not driven:** the `Exporter` menu (and the explorer context menu): Avalonia popups do not show in `Screen` or `PrintWindow` captures here and no secondary window is exposed (`Focus-Dialog`: none), so the item list was not seen (P19-15 saw `SVG vectoriel`); pointer-over on a menu item and the pressed state; the hover state of buttons was moved over `Supprimer` but no difference could be read; English.
- Nothing left running (app stopped, `dotnet build-server shutdown`, scratch dirs and publish folder removed).
