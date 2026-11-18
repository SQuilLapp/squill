# P18-32 - Documentation: ADR 0015, changelog and README

Status: done
Depends on: P18-31, P18-28, P18-29, P18-30

## Goal
Record the phase: decisions, limits, what stays unbuilt.

## Design decisions (final)
- `docs/adr/0015-remaining-mockups.md` (format of ADR 0014): context (the five mockups of `visual-spec.md` section 10), decisions (recipe 1 scope table, same-dialect compare, PostgreSQL-only privileges, CSV-only import, polling rules, every write through the statement runner and the production confirmation, the `ItemsSource` rule), consequences and not built.
- `CHANGELOG.md`: add the entries under the next version heading following the file's existing format (read it; the release notes extraction of P12-04 parses it, keep its shape). Never edit `eng/Version.props`.
- `README.md` feature list, `docs/architecture.md` (new folders `Squill.Core/Activity`, `Security`, `Schema/Compare`, `Squill.Sql/Migration`, `Security`, `Squill.Presentation/Home`, `Compare`, `Activity`, `Security`, `Import`, new capability interfaces `IActivityReader`, `ISecurityReader`), `docs/design/visual-spec.md` section 10 rewritten ("what the app still does not have": rows of M01 / M09 / M10 / M11 / M12 not built, M14 items) and sections for M01, M09, M10, M11, M12 in the style of sections 13 and 14 with the numbers measured by the head cards, `docs/design/mockups/README.md` table.
- `docs/tasks/README.md`: phase 18 status line set to done, index statuses.

## Tasks
- [x] ADR, changelog, README, architecture, visual spec, mockups README, tasks README.

## Acceptance criteria
- Build and tests green (docs only, but run them); the release notes extraction test still passes; links resolve.

## Suggested commit message
Document phase 18: home, compare, sessions, security and import
