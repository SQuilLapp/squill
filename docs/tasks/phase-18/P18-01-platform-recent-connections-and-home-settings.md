# P18-01 - Platform: recent connections and home settings

Status: todo
Depends on: none

## Goal
The data the M01 home page needs and the app does not store today: when each connection was last opened, and the home settings. Recipe 1, 3.

## Design decisions (final)
- `src/Squill.Platform/Profiles/IRecentConnectionsStore` + `JsonRecentConnectionsStore` (file `recent.json` in the app data directory, like `JsonProfileStore`; source-generated JSON context): `Task<IReadOnlyList<RecentConnection>> ListAsync(CT)` newest first, `Task TouchAsync(Guid profileId, DateTimeOffset openedAt, CT)` (moves to front, keeps at most 20), `Task ForgetAsync(Guid profileId, CT)`. `RecentConnection(Guid ProfileId, DateTimeOffset LastOpened)`. A missing or corrupt file reads as empty and is rewritten on the next touch (never throws). Same atomic write pattern as the other stores.
- `AppSettings` (P2-07) gains `ShowHomeAtStartup` (default `true`) and `HomeTipIndex` (int, default 0), `HomeTipsHidden` (bool, default false); old settings files without them load with the defaults.
- No version, tab count or server version is stored (not drawn, recipe 1).
- `ProfileStore` deletion calls `ForgetAsync` where profiles are deleted (Presentation, P18-02), not inside the store.

## Out of scope
- Any UI, the view-model (P18-02), projects, tips content.

## Tasks
- [ ] Contract, JSON store, JSON context, `AppSettings` fields, factory/registration next to the other stores (`AppComposition` wiring is P18-02).
- [ ] Tests in `tests/Squill.Platform.Tests`: touch orders newest first, touching twice keeps one entry, 21st entry drops the oldest, corrupt file reads as empty, old settings file loads with defaults, round trip of the new settings.

## Acceptance criteria
- Common acceptance (recipe 7), no head touched.

## Suggested commit message
Add the recent connections store and the home settings
