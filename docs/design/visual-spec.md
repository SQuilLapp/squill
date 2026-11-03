# SQuilL visual specification (phase 9)

Source of truth: the PNGs in `docs/design/mockups/` (see its README). This file turns them into numbers and decisions so that a card can be implemented without guessing. When this file and a PNG disagree, **the PNG wins** and the card report says so.

## 1. How to read the mockups

- The PNGs are 2880 x 1800 px = 1440 x 900 logical px at 2x. **Logical px = image px / 2.**
- Measurements below were read on a downscaled preview and are good to about +-2 logical px. To measure precisely, crop and enlarge a region of the PNG with PowerShell (`System.Drawing`, save into the scratchpad directory) and read the crop with the Read tool. Never guess a size that this file gives as "about".
- Colors are never copied from pixels into code: use the token brushes (`SquillXxxBrush`). If a color seen in a mockup has no token, the card says so and P9-01 adds it to `tokens/squill.tokens.json`.
- Light = `clair`, dark = `sombre`. Every card is verified in both.

## 2. Decisions (final)

| Topic | Decision |
|---|---|
| UI language | **French**, as drawn. Phase 9 introduces a string catalog (P9-02); all user-visible text of the screens in scope comes from it. Keys are English identifiers, values are the French strings drawn in the mockup. No language switch and no English catalog in phase 9. Strings of the mockup that belong to a feature the app does not have are not added. |
| Fonts | UI: token `font.ui` (Segoe UI Variable first, as the Settings mockup M14 shows: "Segoe UI Variable (système)"). The mockups were rendered with Inter as a stand-in, so Inter is bundled only as the fallback. Code, grid, identifiers, keyboard hints: **JetBrains Mono**, bundled. Wordmark: **Inter Tight**, bundled. |
| Features missing from the app | Phase 9 changes appearance and layout, not features. A mockup element whose feature or data does not exist is **not drawn** (list in section 10). The only data additions are the ones of P9-03. |
| Unavailable data inside a drawn element | Omit the element, never print a placeholder or invented value. |
| Density | Standard (row 26, control 28) is the drawn density. Compact/Comfortable keep working through the existing tokens. |
| Window size for comparison | Content area 1440 x 900 logical px. |
| Platform chrome | The mockup draws its own title bar with the menu inside it. WinUI: extend into the title bar (`ExtendsContentIntoTitleBar`, `SetTitleBar`) and keep the system caption buttons. Avalonia: `ExtendClientAreaToDecorationsHint` with system chrome buttons kept on Windows, normal chrome elsewhere. |

## 3. Global metrics (logical px, +-2)

| Element | Value |
|---|---|
| Title/menu bar | height 41; app icon 16 at x=14; menu items 13 px, spacing ~20; bottom border `border-subtle` |
| Command search (title bar) | horizontally centered, width 438, height 27, top 7, radius 6, placeholder `fg-3`, right `Ctrl+K` keyboard chip |
| Activity rail | width 45, background `bg-surface`, right border `border-subtle`; icons 20 px, vertical pitch 40, first center y=63 (below the title bar); active item: 36 x 36 rounded 6 `accent-soft` plus a 3 px `accent` bar on the left edge; settings gear pinned to the bottom |
| Explorer panel | width 270 (x 45..315), background `bg-surface`; header 36 high with caps label 11 px letter-spaced `fg-3` and icon buttons on the right; filter box height 27, radius 6; tree row height 24, indent 16 per level, chevron 12, counts right-aligned `fg-3` |
| Connection row in tree | engine badge 18 x 18 radius 4 with two letters 9 px bold white; name 13 semibold; environment pill (11 px caps semibold, radius 3, padding 6 x 1, `env-*-soft` background and `env-*` text); connected indicator 7 px `success` dot at the right |
| Tab strip | y 41..77 (36 high); tab: 8 px environment-colored square + name 13 px; active tab `bg-base` with a 2 px top border in the environment color (`accent` if no environment), unsaved dot 6 px, close x on hover/active; "+" button at the end; tabs separated by 1 px `border-subtle` |
| Query toolbar | y 77..115, padding 8; button height 28, radius 6, gap 6; primary = `accent` fill, `on-accent` text, kbd hint in mono 11 at 70 % opacity; secondary = `bg-raised`, 1 px `border`; "Valider" = `success` text and border, "Annuler" = `danger` text and border; disabled = `fg-disabled` |
| Editor | gutter 52 wide, numbers right-aligned `fg-3` mono 13/20; 3 px left stripe in the environment color along the editor; current statement background `selection` with a small accent triangle in the gutter; font `mono-editor` |
| Results panel header | 32 high; result tabs: active = pill `bg-active` radius 6, label 13 semibold + row count mono 12 `fg-3`; right: search box 220 x 26 and "Exporter" dropdown button 26 high |
| Grid | header row 26: name 13 semibold + type 11 mono `fg-3` after the name, 1 px `border-subtle` separators, sort arrow `accent`; data rows 26 (density), mono 12 (`mono-grid`), numbers right-aligned, `NULL` italic `cell-null`; selected row `selection`; row-number column 36 wide right-aligned `fg-3`; footer 26 high text 12 `fg-2` |
| Right panel (properties, history) | width 264, background `bg-surface`, left border `border-subtle`; caps header 11 px |
| Status bar | height 26, background `bg-surface`, top border `border-subtle`; first segment = environment block (`env-*` fill, `on-env-*` text, caps 12 bold, padding 14, warning triangle icon for production); segments separated by 1 px `border-subtle`, padding 12; identifiers in mono 12; right group: cursor position, encoding, server version |
| Cards / panels | radius 8, 1 px `border-subtle`, background `bg-raised`, padding 14 |
| Text fields | height 30, radius 6, 1 px `border`, background `bg-raised`; focus = 2 px `focus` border; label 13 `fg-2` above the field with 6 px gap |
| Pills/badges | 11 px caps semibold, radius 3 (tags: radius full) |
| Keyboard chip | mono 11, 1 px `border`, radius 4, padding 5 x 1 |

## 4. Screen M03 - Workspace (`M03-Workspace-*`)

Layout: title bar (41) / activity rail (45) + explorer (270) + main area + properties panel (264) / status bar (26). The main area = tab strip (36) + query toolbar (38) + editor + results panel; the splitter between editor and results sits at about 62 % of the main-area height (result header at y=545). The properties panel shows the explorer's selected object: icon + name 15 semibold, subtitle `<Kind> · <schema>` (e.g. `Table · public`) `fg-3`; then `Colonnes · <n>` with one line per column (key glyph: PK = accent diamond, FK = info arrow, other = small dot; name in mono; type right-aligned mono `fg-3`), `Index · <n>` with index names in mono, footer link `Ouvrir l'éditeur de table →` in `accent-text`. Row estimate, size, owner and last VACUUM lines are **not drawn** (no data).

Strings: `Fichier`, `Édition`, `Affichage`, `Requête`, `Base de données`, `Outils`, `Aide`, `Rechercher objets, fichiers, actions...`, `EXPLORATEUR`, `Filtrer les objets`, `Tables`, `Vues`, `Vues matérialisées`, `Fonctions`, `Séquences`, `PROPRIÉTÉS`, `Colonnes · {0}`, `Index · {0}`, `Ouvrir l'éditeur de table →`, `Résultat {0}`, `Messages`, `Plan`, `Rechercher`, `Exporter`, `Lecture seule · {0}`, `PRODUCTION`, `STAGING`, `DEV`, `LOCAL`, `Transaction ouverte · {0} instruction(s) en attente`, `{0} lignes · {1} ms`, `Ln {0}, Col {1}`, `UTF-8`. (Menu items that have no command stay visible but disabled; the menu content is defined by P9-06.)

## 5. Screen M04 - Editor

Toolbar as section 3. Buttons drawn: `Exécuter` + `Ctrl+Entrée`, `Script` + `Ctrl+Maj+Entrée`, `Expliquer`, `Arrêter`, then (only when the transaction feature exists, see section 10) `Auto-commit` toggle, `Valider`, `Annuler`. Right icons: format (`Ctrl+Maj+F` tooltip) and history. Inline diagnostic: red wavy underline plus an inline pill after the line end (`danger-soft` fill, `danger` text 12) with the message. Completion popup: width ~ 500, radius 8, `bg-overlay`, `shadow-3`, rows 26: kind icon 16, label mono 13, detail `fg-3` right-aligned, footer hint line 24 high with keyboard chips. Signature help: card under the caret, `bg-overlay`, parameter in bold accent, doc text 12 `fg-2`. Right panel `Historique` / `Snippets` (tabs 13, active underline `accent`): search box, entries = timestamp mono 11 `fg-3` + duration + row count right, SQL 2 lines mono 12 truncated with ellipsis, error entries show `erreur` in `danger`; section `SNIPPETS ÉPINGLÉS` at the bottom (trigger in mono accent + description). Parameters panel under the editor: title `Paramètres de la requête` + muted count, rows = `:name` mono, type dropdown 165 wide, value field, `Transmettre NULL` checkbox, validation `Valeur requise` in `danger`; buttons `Ignorer` (ghost) and `Exécuter avec ces valeurs` (primary).

Strings: `Exécuter`, `Script`, `Expliquer`, `Arrêter`, `Historique`, `Snippets`, `Rechercher dans l'historique`, `SNIPPETS ÉPINGLÉS`, `Paramètres de la requête`, `{0} détectés · valeurs mémorisées pour ce fichier`, `Ignorer`, `Exécuter avec ces valeurs`, `Valeur requise`, `Transmettre NULL`, `Tab accepter`, `naviguer`, `Échap fermer`, `erreur`, `ligne`, `lignes`.

## 6. Screen M05 - Grid

Order from the tab strip down: query tabs (M05 shows `clients.sql` and `ventes.sql`), editor (collapsed to 2 lines here), results header, optional filter bar, pending-edits banner, grid, footer. Result tab label: `clients` + mono count `1 208`. Read-write marker next to the tabs: `Modifiable · clé primaire id` in `success` with a pencil icon; read-only: lock icon + `Lecture seule · <reason>` in `fg-3`.
Pending-edits banner (36 high, `info-soft` background, pencil icon `info`): `3 modifications en attente` semibold + muted detail `1 mise à jour · 1 insertion · 1 suppression`; buttons right: `Aperçu SQL`, `Annuler`, `Valider` (primary).
Row states (tokens): modified = `cell-modified` row tint, added = `cell-added` tint and `+` in the number column with a 3 px `success` left bar, deleted = `cell-deleted` tint, strikethrough text, `-` in the number column and a 3 px `danger` left bar; added row placeholders `(auto)`, `NULL`, `DEFAULT` in italic `fg-3`; the cell in edit = 2 px `accent` border over the cell, text field inside, row selected = `selection`. Primary key column header has an accent diamond before the name; sort arrow after the name; filtered column funnel icon `accent`.
Export menu (`Exporter ⌄`): popup 300 wide, radius 8, `bg-overlay`, `shadow-3`, title `EXPORTER <n> LIGNES` caps 11 `fg-3`, items 28 high (name 13 left, extension mono 12 `fg-3` right): `CSV` `.csv · séparateur ;`, `JSON` `.json`. Items for formats that do not exist are not drawn.
Footer: `Lignes 1–{0} sur {1} chargées` and, on the right, `{0} cellules sélectionnées` when applicable.
Not drawn (no feature): filter bar, column menu (sort/filter/freeze/hide/fit/copy), row side panel, value filter, block size, infinite scroll text, cell sum.

Strings: `Modifiable · clé primaire {0}`, `{0} modification(s) en attente`, `{0} mise(s) à jour`, `{0} insertion(s)`, `{0} suppression(s)`, `Aperçu SQL`, `Annuler`, `Valider`, `(auto)`, `DEFAULT`, `Exporter`, `EXPORTER {0} LIGNES`, `Lignes 1–{0} sur {1} chargées`, `{0} cellules sélectionnées`.

## 7. Screen M02 - Connections (`M02-Connexions-*`)

Two-pane window, title `Connexions — SQuilL`. Left pane 292 wide, `bg-surface`: top row = search box (height 27, `Rechercher`) + two 27 x 27 icon buttons (new connection, new folder); groups = chevron + folder icon + name 13 semibold + count right; items row 31 high indented 24: engine badge 18, name, 8 px environment square at the right (color by environment); selected item = `accent-soft` row radius 6; bottom `ÉTIQUETTES` section with tag chips (height 26, radius full, `bg-hover`). Right pane padding 32: header = engine badge 32 x 32 radius 6, name 20 semibold, subtitle `Nouvelle connexion · non enregistrée` 13 `fg-3`, environment pill; `MOTEUR` caps label then a grid of engine tiles (6 per row, tile 113 x 52, radius 6, 1 px `border`, gap 7, badge 20 centered above the name 13; selected = 2 px `accent` border + `accent-soft`); right column (width 339) `ENVIRONNEMENT` with 2 x 2 tiles (34 high, 8 px colored square + name; selected = 2 px border in the environment color + its soft background) and a hint text 12 `fg-3`; tabs `Général`, `SSH`, `SSL`, `Proxy`, `Avancé`, `Pilote` (underline 2 px `accent`, status dot 6 px `success` when configured); form = labelled fields (section 3), pairs side by side (`Hôte`/`Port`, `Utilisateur`/`Authentification`), `Mot de passe` with a lock hint at the right `Dans le Gestionnaire d'identification` in `success`, `URL générée` in a mono field with a copy button, checkbox row; right cards (`Tunnel SSH`, `SSL / TLS`, `Pilote`) = cards with title 13 semibold, state pill, `Modifier` link `accent-text`, mono details. Footer (60 high, top border): result bar left (`success-soft`, `Connexion réussie` + mono details) and buttons right: `Tester` + kbd `Ctrl+Entrée`, `Annuler`, `Enregistrer et se connecter` (primary).
Only the 4 dialects of `DatabaseDialect` get an engine tile; only tabs for existing settings (`Général`, `SSH`) are drawn; Proxy, SSL, Avancé, Pilote tabs, tags, generated URL and `Lecture seule`/startup checkboxes are not drawn.

Strings: `Connexions — SQuilL`, `Rechercher`, `MOTEUR`, `ENVIRONNEMENT`, `Production`, `Staging`, `Développement`, `Local`, `Général`, `SSH`, `Nom de la connexion`, `Dossier`, `Hôte`, `Port`, `Base de données`, `Utilisateur`, `Authentification`, `Mot de passe`, `Tunnel SSH`, `Activé`, `Modifier`, `Tester`, `Annuler`, `Enregistrer et se connecter`, `Connexion réussie`, `Nouvelle connexion · non enregistrée`, `Production active la confirmation renforcée et la transaction manuelle par défaut.`

## 8. Screen M07 - Table designer

Tab: table icon + `commandes` + muted `Structure` + unsaved dot + close. Header: table icon 32, name 18 semibold + environment pill, subtitle 13 `fg-3` (`<connection> · <database> · <schema>`), right side draft state (`2 modifications en brouillon`, dot `info`) and buttons `Annuler les modifications` (secondary) and `Appliquer` + `Ctrl+S` (primary). Sub-tabs with count in muted mono: `Colonnes 7`, `Index 3`, `Clés étrangères 1` (the others are not drawn). Toolbar: `Ajouter une colonne` + kbd `Ctrl+Inser`, `Supprimer`, up/down arrows, right hint `F2 modifier · Alt+↑/↓ réordonner`. Columns table: bordered card radius 8; header 28 high `bg-surface` with `PK`, `Nom`, `Type`, `NULL`, `Défaut`, `FK`, `Commentaire`; rows 30; name mono, type mono in `syn-type` color, checkboxes 16; row tags `MODIFIÉE` (`info`) and `NOUVELLE` (`success`) 10 px caps bold after the name with row tint `cell-modified`/`cell-added`. Below: `Colonne sélectionnée · <name>` card (label column 70, fields 30 high). Right `APERÇU DDL` panel 264 wide: caps header `généré · lecture seule`, copy and open icons, highlighted DDL (mono 13/20 with `syn-*`), footer checkboxes `Exécuter dans une transaction`, `Générer aussi le script de retour arrière`. Not drawn: `Impact estimé` card, tabs Contraintes/Déclencheurs/DDL, constraint field, rollback checkbox unless the view-model has it.

## 9. Screens M08 (ER) and M14 (settings)

M08: toolbar 44 high: `Mise en page auto` button, segmented `En couches` / `Orthogonal` / `Libre` (only those the layout engine supports), zoom `–` `85 %` `+`, `Ajuster` + `Ctrl+0`, checkboxes `Types`, `Commentaires`, `Clés seulement si replié`, right `Exporter ⌄` (menu `Image PNG`, `SVG vectoriel`, `PDF`, `Mermaid (presse-papiers)`: only SVG exists, drawn as `SVG vectoriel`). Left panel 220 wide: caps `TABLES · 6 / 24`, rows 24 with checkbox + table icon + mono name, hint at the bottom (12 `fg-3`). Canvas: `bg-base` with a 16 px dot grid (1 px dots `border-subtle`). Node: width ~210, radius 8, 1 px `border`, `bg-raised`; header 33 high with table icon, mono semibold name and right-aligned row count `fg-3`; rows 24: `PK` (accent) / `FK` (info) 9 px bold tag in a 22 px column, mono column name, right-aligned type in `syn-type`; selected row `accent-soft`. Relations: 1.5 px `border-strong`, `accent` when highlighted, crow's-foot ends; FK tooltip card (`bg-overlay`, name bold, text 12, rule line caps 11 `fg-3`). Minimap bottom-right 209 x 108, `bg-surface`, nodes as `border-strong` blocks, viewport rectangle `accent`. Status bar right: `Molette : zoom · Espace + glisser : déplacer`.
M14: window title `Paramètres — SQuilL` with a back arrow. Left nav 260 wide: search box `Rechercher un paramètre`, items 36 high icon + label (`Apparence`, `Éditeur`, `Grille de résultats`, `Formatage SQL`, `Raccourcis clavier`, `Connexions et sécurité`, `Pilotes`, `Avancé`; only pages with existing settings are drawn), selected = `bg-active` radius 6 + 3 px `accent` bar. Content: page title 28/36 semibold (`Apparence`), setting cards (radius 8, `bg-raised`, 1 px `border-subtle`) holding rows 56 high: icon 20, title 14 semibold, description 13 `fg-3`, control right. Theme: row with three 186 x 119 preview cards (`Système`, `Clair`, `Sombre`, radio under, selected = 2 px `accent` border). Language row added by phase 10 (not in the mockup): first card of the Appearance page, same 56 high row, title left, 180 wide `ComboBox` right (`Par défaut du système` / `Français` / `English`), restart note under it in `fg-3` while the choice differs from the active language. Not drawn: accent color, window material, density, UI font, ligatures, animations, shortcuts, drivers (no settings behind them).

## 10. What the app does not have (not drawn in phase 9)

M01 start page, M06 plan, M09 schema compare, M10 transfer, M11 privileges, M12 sessions, M13 command palette, M15 production confirmation dialog, the filter bar, column menu and row panel of M05, tags/SSL/proxy/driver tabs of M02, impact estimate of M07, shortcuts/drivers/accent/material/density of M14, auto-commit/manual transaction controls of the toolbar (Valider/Annuler of M03/M04). Each is a feature, not a style, and needs its own phase.

## 11. Verification procedure (every head card)

1. Build the head, create the sample data with the seed of P9-01 (`tools/visual/`), launch the app (`rtk bypass dotnet run --project src/Squill.App.<Head> -c Release` or the AOT executable).
2. Capture the window with `tools/visual/Capture-Window.ps1` (P9-01) in light and dark at a 1440 x 900 logical content size, then build the side-by-side with `tools/visual/Compare-Mockup.ps1 -Mockup <png> -Capture <png>`; read the composite with the Read tool and fix every visible difference listed in the card.
3. If the screen cannot be reached or driven (no UI automation for a dialog, a feature without data), the card report states exactly what was not compared and what the owner must check by hand. Never claim a visual match that was not looked at.
4. Close every process started.

## 12. Status (end of phase 9)

Collected from the card reports and commit messages. "Matched" means looked at against the mockup in at least one theme; see [ADR 0006](../adr/0006-visual-conformance.md) for what was never driven by hand (hover, pressed and focus states, most popups) and for the themes not captured.

| Screen | Avalonia | WinUI |
|---|---|---|
| Window chrome, rail, status bar (M03) | Matched (P9-07) | Partly: unconnected state matched; connected state and menu popups not compared; menu items about 8 px tighter (P9-09) |
| Explorer, tabs, properties (M03) | Matched, single tree with counts (P9-08, P9-26, P9-27) | Partly: rows 28 high instead of 24, `PRODUCTION`/`STAGING` pills instead of `PROD`/`DEV`, unsaved dot and hover close not looked at (P9-10, P9-26, P9-27) |
| Editor, toolbar, history (M04) | Partly: no wavy underline, inline pill, margin triangle, statement background, popup styling; no `Expliquer`, auto-commit; parameters prompt stays modal; light read on the capture alone (P9-11) | Partly: same gaps; dark only, light and diff not read in detail; parameters dialog and environment stripe not seen (P9-12) |
| Results panel and grid (M05) | Matched; filter bar, column menu, row panel not drawn (P9-13, P9-14) | Matched on the captures taken; same omissions (P9-15, P9-16) |
| Connections (M02) | Matched; tags, SSL, proxy, driver tabs not drawn (P9-17) | Matched; same omissions (P9-18) |
| Table designer (M07) | Partly: headless render only, live window not driven; `Commentaire`, impact, Contraintes/Déclencheurs/DDL tabs not drawn (P9-19) | Partly: dark only; row tags, draft state, issue badges not exercised (P9-20) |
| ER diagram (M08) | Partly: row counts, comment and layout options, PNG/PDF/Mermaid export not drawn; relation tooltip and hover not driven (P9-21) | Partly: same; capture narrower than 1440 px, `Exporter` item and hops tooltip not checked (P9-22) |
| Settings (M14) | Four pages in scope done after the mockup (no capture comparison recorded); language row added by phase 10 (P10-05); shortcuts, drivers, accent, material, density not drawn (P9-23) | Same (P9-24) |
| Shared control styles | Gallery captured (P9-05) | Gallery in static states only; popup shadow is the WinUI default; popups and pointer-over/pressed not driven (P9-06) |
