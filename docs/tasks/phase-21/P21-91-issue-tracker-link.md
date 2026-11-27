# P21-91 - Help menu: report an issue (link to the issue tracker)

Status: todo
Depends on: none

## Goal
Low priority. A `Help > Report an issue...` entry opens the project's GitHub new-issue page with the app version and OS pre-filled, like DataGrip and DBeaver link to their trackers.

Exists today: the About dialog (P12-12, P12-13, P12-15) with the version (`AppVersion.Current`); `crash.log` location known (WinUI); release notes link to `squillapp/squill`.

## Design decisions (final)
- URL `https://github.com/squillapp/squill/issues/new` with `title` empty and `body` containing only: app version, head (WinUI/Avalonia), OS name and version, language. **No** connection names, paths, SQL or crash log content in the URL (privacy rules); the dialog says the crash log path and suggests attaching it by hand.
- A confirmation shows the exact text before opening the browser (recipe 6).
- Both heads: one menu item and the confirmation. One card.

## Out of scope
- In-app bug submission, telemetry.

## Tasks
- [ ] Tests of the URL builder (encoding, no personal data); both heads; drive (recipe 3) up to the confirmation (do not open the browser in the drive, record `not driven (browser)`).

## Acceptance criteria
- Recipe 2 and 3 acceptance for both heads.

## Risks
- None.

## Open points (defaults)
- None.

## Suggested commit message
Add a Report an issue link to the Help menu
