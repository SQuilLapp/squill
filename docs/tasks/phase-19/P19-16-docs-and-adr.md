# P19-16 - Docs, ADR 0016 and changelog for phase 19

Status: done
Depends on: P19-01 ... P19-15

## Goal
Record what phase 19 fixed, what it looked at, and what is still open, so the owner's checklists are the only thing left.

## Design decisions (final)
- `docs/adr/0016-debug-and-polish.md` (format of ADR 0015: Status accepted, date, Context, Decision, Consequences, Known limits, Not built): the keyword quoting rule, events raised on the UI thread (or "no production producer found", per P19-02), the rows-cut notice and the 400-row limit as a conscious `shortcut:`, the AOT warning guard and its baseline, the capture tool contract, the rule that agent passes never tick the owner's boxes. Known limits: what stayed `not driven` in the verification cards (copy from their Done notes), Linux / macOS never run.
- `CHANGELOG.md` under `## [Unreleased]` (format rules at the top of the file; no tables, images or nested lists): Fixed (names that are SQL keywords are quoted in completion and GRANT scripts; the sessions and privileges lists say when rows are not shown; fixes found by the capture passes, one bullet each, from the cards' Done notes), Changed only if a visible wording or style changed.
- `docs/tasks/README.md`: phase 19 section and index rows are written by this phase's first commit; this card sets each finished card to `done`, adds `P19-17` onwards for the cards the verification passes proposed (index row and card file), and rewrites the phase header status line.
- `docs/verification/new-screens.md`, `transactions.md`, `plan.md`, `completion.md`, `grid-filter-palette.md`: only the `Status:` line (what the agent passes did), no tick, no step text edit. `docs/design/visual-spec.md` section 12: remove "not driven" wording that a pass resolved, keep the rest. `docs/design/mockups/README.md` "In the app today" table: update the rows that changed. `docs/poc/aot-drivers.md`: one line linking `eng/check-aot-warnings.ps1`. `docs/testing.md` already updated by P19-06.
- Check every relative link added (`rtk grep` for the file names).

## Tasks
- [x] ADR, changelog, index and status lines, spec rows, mockups README.
- [x] Link check by hand (open each new link target path).

## Acceptance criteria
- Docs only: no build required, `rtk git status` shows only documentation files; the index table has one row per card file in `docs/tasks/phase-19/`, and the card statuses match the files.

## Suggested commit message
Document phase 19: debug, verification passes and polish

## Done notes
- ADR 0016, changelog (Fixed), `Status:` lines of the five checklists (no box ticked), mockups README rows M01 and M09-M12, one line in `docs/poc/aot-drivers.md`. `docs/design/visual-spec.md` needed no change: P19-09 to P19-11 already updated it. Links checked by path. P19-13, P19-15 and P19-20 stay partial; unverified items are in ADR 0016 Known limits.
