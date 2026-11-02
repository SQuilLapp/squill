# P9-25 - Documentation: visual conformance ADR, README and architecture note

Status: todo
Depends on: P9-07 ... P9-24

## Goal
Record what phase 9 decided and what remains.

## Design decisions (final)
- ADR `docs/adr/00NN-visual-conformance.md` (next free number): decisions of `docs/design/visual-spec.md` section 2 (French UI catalog, fonts, features not drawn), how fidelity is verified (tools/visual), known gaps.
- `docs/design/visual-spec.md`: add a section 12 `Status` listing, per screen and head, matched / partly matched / not matched with the reasons given in the card reports (collect them from the cards' reports and the git log of the phase).
- README: screenshots section updated (replace any old screenshot by captures taken with the P9-02 tools, light and dark, M03 and M05 equivalents), supported languages line (French only).
- `docs/architecture.md`: a short paragraph on the string catalog, the font bundling and the environment/folder profile fields.
- List the follow-up feature phases implied by `visual-spec.md` section 10 (start page, plan, compare, transfer, privileges, sessions, palette, production confirmation) in the ADR as proposals only.

## Out of scope
- Code changes.

## Tasks
- [ ] Write the ADR, the status section, README and architecture edits.

## Acceptance criteria
- Docs only: `rtk git diff --stat` touches only `docs/` and `README.md`.
- Links in the new documents resolve (check with a grep of the referenced paths).
- No process left running (app, `dotnet build-server`, AOT executable).

## Suggested commit message
Document the visual conformance phase
