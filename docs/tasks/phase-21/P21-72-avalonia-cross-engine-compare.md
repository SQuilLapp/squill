# P21-72 - Avalonia: mapping review step and equivalent rows in the schema compare view

Status: todo
Depends on: P21-70, P21-71

## Goal
Same as P21-71 in the Avalonia head; then README, ADR 0015 and `docs/ideas.md` updates (the first pair is built; the rest of the analysis stays open).

Exists today: `Views/SchemaCompareView.cs` (Avalonia).

## Design decisions (final)
- Same panel and rows as P21-71.

## Out of scope
- WinUI.

## Tasks
- [ ] Headless tests; publish and drive (recipe 3).
- [ ] README compare line, ADR 0015 note, the index section "Cross-engine schema compare" gets a line "first pair built by P21-67 ... P21-72", `CHANGELOG.md` line.

## Acceptance criteria
- Recipe 2 and 3 acceptance (Avalonia).

## Risks
- None beyond P21-71.

## Open points (defaults)
- Owner: which pair next (default PostgreSQL -> SQLite or MySQL -> PostgreSQL, decided later).

## Suggested commit message
Show the cross-engine mapping review in the Avalonia schema compare view
