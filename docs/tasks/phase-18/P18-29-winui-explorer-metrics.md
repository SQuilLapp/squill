# P18-29 - WinUI head: explorer metrics and environment pills

Status: todo
Depends on: none

## Goal
Remove the two WinUI differences recorded in `visual-spec.md` section 12 for M03: explorer tree rows 28 high instead of 24, and environment pills reading `PRODUCTION` / `STAGING` where the mockup draws the short `PROD` / `DEV`.

## Design decisions (final)
- First re-measure on the current build (the spec row dates from phase 9; phases 11 to 17 changed the explorer). If a difference is already gone, say so in the report and drop it.
- Row height 24 (token `row.explorer` or the value of `visual-spec.md` section 3; add a token only if none exists), indent 16 per level, chevron 12, counts right-aligned `fg-3`; the keyboard navigation, selection and context menus keep working at the new height; touch targets are not a requirement on desktop.
- Pill text: `PROD`, `STAGING`, `DEV`, `LOCAL` through the new short keys `home.env.*` of P18-02 if that card is done, else new keys `env.short.*` (fr + en; English `PROD`, `STAGING`, `DEV`, `LOCAL` as in the mockup); the **status bar block** keeps the long `PRODUCTION` (M03 draws it long there). Avalonia already matches: check that it uses the same keys afterwards.
- The unsaved dot and the hover close button of tabs, listed as "not looked at": look at them now and note the result.

## Out of scope
- Anything else in the status table.

## Tasks
- [ ] Measure, fix row height and pills, keys, tests (an explorer view-model test is not enough: add a head test that asserts the row height constant, if the harness allows).
- [ ] Update the M03 WinUI row of `visual-spec.md` section 12.

## Acceptance criteria
- Common acceptance (recipe 7) with the WinUI AOT publish; before / after capture of the explorer next to `M03-Workspace-*`.

## Suggested commit message
Match the WinUI explorer row height and environment pills to the mockup
