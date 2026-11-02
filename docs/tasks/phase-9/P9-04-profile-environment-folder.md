# P9-04 - Core and Platform: connection environment and folder

Status: todo
Depends on: none

## Goal
The mockups color tabs, pills, stripes and the status bar by environment and group connections by folder; the profile model has neither. Add them (data only).

## Design decisions (final)
- In `Squill.Core`: enum `ConnectionEnvironment { None, Production, Staging, Development, Local }` and two new nullable-by-default members on `ConnectionProfile`: `ConnectionEnvironment Environment` (default `None`) and `string? Folder`. Keep the record constructor source compatible by adding them as the last parameters with defaults; fix every call site the compiler reports.
- `Squill.Platform` JSON profile store (source-generated context): serialize both, read old files without them (missing = defaults), a round-trip test and an old-format test.
- `Squill.Presentation`: `ConnectionEditorViewModel` exposes `Environment` and `Folder` (editable, validated: folder max 64 chars, trimmed, empty = null), `ConnectionListViewModel` exposes items grouped by folder (`Groups`: folder name or null, items ordered by name; groups ordered by folder name, ungrouped last) and each item exposes `Environment`. `SessionManager`/workspace expose `ActiveEnvironment` for the status bar and tabs (None when not connected).
- Production defaults: nothing behaves differently yet (no confirmation, no manual transaction); this card is data and display state only. Say so in the XML doc of `ConnectionEnvironment`.

## Out of scope
- Tags, read-only flag, SSL/proxy settings, any behavior tied to production, any view.

## Tasks
- [ ] Implement model, store, view-model changes.
- [ ] Tests in `Squill.Core.Tests`, `Squill.Platform.Tests`, `Squill.Presentation.Tests` (old profile file loads with defaults, grouping order, editor validation, `ActiveEnvironment` follows the active session).

## Acceptance criteria
- `rtk bypass dotnet build Squill.Windows.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.Windows.slnx -c Release` is green.
- A profile file written by the previous version still loads (test).
- No process left running (app, `dotnet build-server`, AOT executable).

## Suggested commit message
Add environment and folder to connection profiles
