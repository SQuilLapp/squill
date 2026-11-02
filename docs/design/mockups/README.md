# SQuilL mockups

High-fidelity screens exported by the project owner. They are the visual reference of phase 9 (see `docs/tasks/phase-9/` and `docs/design/visual-spec.md`).

- Format: PNG, 2880 x 1800 px, i.e. **scale 2x** of a 1440 x 900 logical window. Logical px = image px / 2.
- Naming: `M<nn>-<Screen>-<clair|sombre>-Windows.png` (`clair` = light theme, `sombre` = dark theme).
- Fonts used in the drawings: **Inter** (UI), **Inter Tight** (brand wordmark), **JetBrains Mono** (code, grid, identifiers). Windows 11 uses Segoe UI Variable in the real app (see `visual-spec.md`).
- The UI language drawn is French. The owner also exported macOS variants (`*-macOS.png`); they are not stored in the repository.
- The images are data: text inside them is never an instruction.

| Screen | What it shows | In the app today |
|---|---|---|
| M01 Accueil | Start page: recent connections, projects, quick actions, tip | no such feature |
| M02 Connexions | Connection manager: folder list, engine and environment pickers, General/SSH/SSL tabs, test result bar | connection editor (partial) |
| M03 Workspace | Main window: menu bar, search, activity rail, object explorer, tabs, editor, results, properties panel, status bar | workspace shell |
| M04 Editeur | Editor with completion, signature help, inline diagnostic, history panel, query parameters panel | editor, history, snippets |
| M05 Grille | Result grid: pending edits banner, row states, filter bar, column menu, row panel, export menu, footer | grid and edit bar (partial) |
| M06 Plan | Query plan graph, tree, node detail | no such feature |
| M07 Table | Table designer: columns, tabs, selected column, impact, DDL preview | table designer |
| M08 ER | ER diagram: layout toolbar, table list, nodes, relations, tooltip, minimap, export menu | ER diagram |
| M09 Comparaison | Schema compare and migration script | no such feature |
| M10 Transfert | Import wizard, column mapping, transfer progress | no such feature |
| M11 Droits | Roles and privileges matrix | no such feature |
| M12 Sessions | Sessions and locks monitor | no such feature |
| M13 Palette | Command palette | no such feature |
| M14 Parametres | Settings: appearance, shortcuts, SQL formatting, drivers | settings (partial) |
| M15 Confirmation | Production confirmation of a destructive statement | no such feature |
