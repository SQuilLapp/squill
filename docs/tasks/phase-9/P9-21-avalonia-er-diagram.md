# P9-21 - Avalonia head: ER diagram view

Status: done
Depends on: P9-08, P9-02

## Goal
The ER diagram tab matches M08: toolbar, table list, node drawing, relations, tooltip, minimap.

## Design decisions (final)
- Read `docs/design/visual-spec.md` sections 3 and 9 (final). Layout and routing stay those of `Squill.Presentation/Diagram`; only drawing and chrome change.
- Toolbar: existing controls re-laid out (auto layout/reset layout button, the 1/2/3 hops segmented control takes the place of the `En couches`/`Orthogonal`/`Libre` segmented control (those layout modes do not exist), fit with the `Ajuster` label, `Exporter` menu with `SVG vectoriel`), zoom control `–  85 %  +` if the canvas exposes zoom (else hidden). Checkbox options (`Types`, `Commentaires`, `Clés seulement si replié`) only for those the node renderer supports (`Types` yes if the existing canvas can hide types, else omit).
- Left table list (220 wide) from the model's tables with a visible checkbox only if the model supports hiding; otherwise a plain list focusing the table on click.
- `ErDiagramCanvas`: node card (radius 8, header 33, rows 24, PK/FK tags, mono names, `syn-type` types, row count in the header when the model has it), relation lines 1.5 px with crow's foot ends, dotted background, hover tooltip for relations with the FK name and the referential actions, minimap bottom-right 209 x 108 if the canvas has a viewport concept (else omit).

## Out of scope
- PNG/PDF/Mermaid export, layout modes not in the engine (no feature).

## Tasks
- [x] Rework `DiagramTabView.axaml` and `Controls/ErDiagramCanvas.cs`.
- [x] Headless tests: toolbar commands bound, canvas renders a 3-table model without exception, pixel sample on a PK tag is `accent`.

## Acceptance criteria
- `rtk bypass dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).
- `rtk dotnet publish src/Squill.App.Avalonia -c Release -r win-x64` succeeds with zero IL2xxx/IL3xxx warnings.
- Visual check (`docs/design/visual-spec.md` section 11): the running app was captured in light and dark with `tools/visual/Capture-Window.ps1`, compared with `M08-ER-clair-Windows.png`, `M08-ER-sombre-Windows.png` through `tools/visual/Compare-Mockup.ps1`, and every visible difference was fixed or listed in the card report with its reason. If a state could not be reached or driven, the report says which and what the owner must check by hand.
- No process left running (app, `dotnet build-server`, AOT executable).

## Suggested commit message
Avalonia: ER diagram view

## Report
- Visual check done in light and dark (AOT exe, sample profile, composites `p21-light-cmp.png` / `p21-dark-cmp.png` in `%TEMP%\squill-visual`). Not drawn on purpose (no data or feature): row counts in node headers, `Commentaires` and `Clés seulement si replié` checkboxes, layout modes, table-list visibility checkboxes (plain list focusing the table), PNG/PDF/Mermaid export, the left drawing hint. The mockup shows the explorer closed and a hovered tooltip; the capture shows the explorer open. The relation tooltip (FK name and columns; no referential actions, the model lacks them) and hover states were not driven: the owner should hover a relation by hand.
- Shared layout defaults changed in `ErLayoutOptions` (header 33, row 24, min width 210) to match M08; Presentation layout/router/SVG tests now pin the old values explicitly. The WinUI canvas picks the new metrics up (P9-22 must draw with them).
