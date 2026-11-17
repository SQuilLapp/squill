# P18-07 - Presentation: line diff for the side-by-side DDL

Status: todo
Depends on: none

## Goal
The aligned, marked lines of the two M09 DDL panes (green `+` on the source side, red `-` on the target side, hatched filler on the side without the line). Recipe 1.

## Design decisions (final)
- `src/Squill.Presentation/Compare/LineDiff.cs`: `LineDiff.Align(string source, string target)` returns `IReadOnlyList<AlignedLine>`; `AlignedLine(int? SourceNumber, string? SourceText, int? TargetNumber, string? TargetText, LineMark Mark)` with `LineMark` `Same | SourceOnly | TargetOnly | Changed`. Algorithm: Myers or an LCS over trimmed-end lines, O(ND); a replaced line (one removed and one added at the same position) is `Changed` and shown on both sides like rows 5 of M09, a run of unequal lengths pads the shorter side with filler lines (`SourceNumber` / `TargetNumber` null).
- Guard: inputs over 20,000 lines each fall back to a position-by-position compare (documented `shortcut:` comment, upgrade when a real case shows up).
- Line numbers count real lines per side, fillers have no number.
- Pure, allocation-light, no UI types.

## Out of scope
- Intra-line highlighting, syntax coloring (the heads reuse the SQL highlighter if it exists for read-only text; otherwise plain mono).

## Tasks
- [ ] `LineDiff` and records.
- [ ] Tests in `tests/Squill.Presentation.Tests/Compare/LineDiffTests.cs`: identical, one line added, one removed, one replaced, the M09 `commandes` example (two added lines in the middle become `SourceOnly` with fillers on the target), empty side, trailing whitespace ignored, CRLF vs LF, large input fallback.

## Acceptance criteria
- Common acceptance (recipe 7), no head touched.

## Suggested commit message
Add the line diff for the schema compare view
