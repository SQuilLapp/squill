# P9-13 - Avalonia head: results panel chrome, pending-edit banner and export menu

Status: todo
Depends on: P9-11, P9-02

## Goal
Everything around the Avalonia grid matches M05: result tabs, read/write marker, pending-edits banner, export menu, footer.

## Design decisions (final)
- Read `docs/design/visual-spec.md` sections 3 and 6 (final).
- The two export buttons become one `Exporter` dropdown (menu `CSV`, `JSON` with their extension text). Same commands as today.
- The edit bar of P8-15 becomes the pending-edits banner of section 6 (same commands: `Aperçu SQL`, `Annuler` = discard, `Valider` = commit; `Add row`/`Delete rows`/`Revert rows` move to small icon buttons in the results header, with tooltips from the catalog), visible only when the editor has pending changes; read/write marker next to the result tabs (`Modifiable · clé primaire {0}` or `Lecture seule · {0}`), issues button kept (error glyph + count) in the banner.
- Result tabs: pill style with the mono count, `Messages` and the progress bar as drawn; footer `Lignes 1–{0} sur {1} chargées`.

## Out of scope
- Grid canvas drawing (P9-14), filter bar, column menu, row panel, search box (no feature).

## Tasks
- [ ] Rework `QueryTabView.axaml` and code-behind of the results panel.
- [ ] Headless tests: banner visible only with pending changes and shows the counts, export menu items invoke the exporters (fake), marker text for editable and read-only results.

## Acceptance criteria
- `rtk bypass dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).
- `rtk dotnet publish src/Squill.App.Avalonia -c Release -r win-x64` succeeds with zero IL2xxx/IL3xxx warnings.
- Visual check (`docs/design/visual-spec.md` section 11): the running app was captured in light and dark with `tools/visual/Capture-Window.ps1`, compared with `M05-Grille-clair-Windows.png`, `M05-Grille-sombre-Windows.png`, `M03-Workspace-*-Windows.png` (results header) through `tools/visual/Compare-Mockup.ps1`, and every visible difference was fixed or listed in the card report with its reason. If a state could not be reached or driven, the report says which and what the owner must check by hand.
- No process left running (app, `dotnet build-server`, AOT executable).

## Suggested commit message
Avalonia: results panel chrome, pending-edit banner and export menu
