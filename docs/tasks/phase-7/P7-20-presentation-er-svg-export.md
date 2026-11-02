# P7-20 - Presentation: ER diagram SVG export

Status: todo
Depends on: P7-19

## Goal
Export the visible diagram as a standalone SVG file, with no UI framework and no extra package.

## Design decisions (final)
- `ErSvgExporter.Render(ErModel model, IReadOnlyDictionary<string, ErBox> boxes, IReadOnlyList<ErEdge> edges, ErLayoutOptions options, bool dark) : string` in `Squill.Presentation.Diagram`; writes the SVG text with `StringBuilder`/`XmlWriter` (UTF-8 declaration not needed, root `<svg xmlns="http://www.w3.org/2000/svg" viewBox=... width height>`, 16 px margin around the content). Text is XML-escaped.
- Drawing: each entity = `<rect>` body, a header `<rect>` with the name, one `<text>` per column (`name`, right-aligned `type`, `PK` marker for primary keys, `FK` for foreign keys); each edge = `<polyline fill="none">` with a small crow's-foot (many side) or a bar (one side) marker at `FromAnchor`/`ToAnchor` drawn with `<path>`/`<line>`, dashed when `IsOptional`. Colours are two fixed palettes (light/dark) taken from `tokens/squill.tokens.json` values (read the file, do not invent colours), placed in one constant per colour inside `ErSvgExporter`.
- `DiagramTabViewModel` gains `ExportSvgCommand` and a settable `bool IsDarkTheme` (default false, set by the head): the command asks `IDialogService.PickSaveFileAsync(profile name, "svg")`, renders with `dark: IsDarkTheme`, writes the file with `File.WriteAllTextAsync`, reports failure through `IDialogService.ShowErrorAsync`; `StatusText` says "Saved <path>". (`IDialogService` is already a constructor parameter, P7-19.)
- Output must be deterministic (same input, same string; invariant culture for numbers, two decimals max).

## Out of scope
- PNG/PDF export, copying the image to the clipboard.

## Tasks
- [ ] Implement the exporter and the command.
- [ ] Tests: output parses as XML (`XDocument.Parse`), contains one `rect` pair per entity and one `polyline` per edge, escaping of `<`, `&` and quotes in names, dashed optional relations, determinism, export command with a fake dialog service and a temp file, cancelled dialog writes nothing.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add ER diagram SVG export
