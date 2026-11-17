# P17-18 - Documentation: grid filter and palette ADR 0014, changelog and README

Status: done
Depends on: P17-17

## Goal
Record what phase 17 built and where it stopped, and update the documents that list features.

## Design decisions (final)
- `docs/adr/0014-grid-filter-and-command-palette.md` (format of ADR 0013): context (M05 and M13 were left out of phases 9 and 16), decisions (the numbered decisions 1 to 17 of [filter-palette-recipe.md](filter-palette-recipe.md), in prose and short: client-side filter over loaded rows, view layer `FilteredGridDataSource`, read-only while filtered, typed operators, sort, value list, unfiltered export; palette as a window overlay, modes by prefix, fuzzy matching, run rules, history is inserted never executed, objects searched in the schema snapshot including tables hidden by "Choose tables"), consequences, known limits (the list of recipe section 3), not done.
- `docs/design/visual-spec.md`: section 6 (M05) drop the filter bar, column menu, `Rechercher` from "Not drawn" and say freeze / hide / fit width, row panel, block size, infinite scroll and cell sum remain not drawn; add a short section `M13 - Command palette` (numbers read from the shipped heads: width, radius, row height, pill and footer styling) and remove the palette from section 10; update the status table row of section 12 for M05 and add a row for M13 (Matched / Partly as the P17-15 / P17-16 reports say, nothing claimed that was not compared).
- `docs/design/mockups/README.md`: M05 and M13 rows (the "no such feature" text for M13).
- `CHANGELOG.md` under `## [Unreleased]` (format rules at the top of the file): Added (grid filter bar, column menu with sort and value filter, grid search; command palette on `Ctrl+K`). Do not touch `eng/Version.props`.
- `README.md` and `docs/architecture.md`: mention the filter and the palette where a feature list or a layer list already exists (`Squill.Presentation/Palette`, `Results/FilteredGridDataSource`), no new sections; update the "not drawn" lines of `docs/adr/0006-visual-conformance.md` only by a one-line pointer to ADR 0014 (do not rewrite history).
- `docs/tasks/README.md`: the phase 17 section already exists; set the status line to `P17-01 ... P17-18 done`.
- Verify each claim against the code with a grep before writing it.

## Out of scope
- New features, ticking the owner's manual checklists.

## Tasks
- [x] ADR, visual spec, mockups README, changelog, README, architecture, task index status.

## Acceptance criteria
- Build and test green (docs only, but run them); every link in the touched files resolves (grep the new file names); `eng/release-notes.ps1` still extracts the `Unreleased` section (run it).

## Suggested commit message
Document the grid filter and the command palette (ADR 0014)
