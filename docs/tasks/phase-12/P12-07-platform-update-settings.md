# P12-07 - Platform: update settings and persisted update state

Status: todo
Depends on: P12-03

## Goal
The user's choices about updates (when to check, which channel) are stored with the other settings, and the app remembers when it last checked, so that no network call happens unless the user opted in and the frequency is due.

## Design decisions (final)
- `UpdateCheckFrequency` enum in `Squill.Platform.Settings`: `NotAsked` (default: nothing was ever chosen, **no automatic network call**), `Never`, `Startup`, `Daily`, `Weekly`. `AppSettings` gets two new trailing parameters with defaults: `UpdateCheckFrequency UpdateFrequency = UpdateCheckFrequency.NotAsked` and `UpdateChannel UpdateChannel = UpdateChannel.Stable` (`UpdateChannel` from Core, P12-03). `AppSettings.Default` and `Clamp` unchanged otherwise. Existing `settings.json` files without the new fields load with the defaults (verify, source-generated context with `UseStringEnumConverter`; add the types to nothing new: they are properties of `AppSettings`).
- `UpdateState` record `(DateTimeOffset? LastCheckUtc, string? SkippedVersion, string? LastSeenVersion)` and `IUpdateStateStore` (`ValueTask<UpdateState> LoadAsync(ct)`, `ValueTask SaveAsync(UpdateState, ct)`) + `JsonUpdateStateStore(AppPaths)` in `Squill.Platform.Updates`, file `update-state.json` in the data directory (single-item list helper like `JsonSettingsStore`, source-generated `UpdateStateJsonContext`). `SkippedVersion` = "do not notify me about this version" (the update banner's *Skip this version*); `LastSeenVersion` = newest version the check returned (to show the banner once per version).
- Pure function `UpdateSchedule.IsDue(UpdateCheckFrequency frequency, DateTimeOffset? lastCheckUtc, DateTimeOffset nowUtc)` (public static, `Squill.Platform.Updates`): `NotAsked` and `Never` -> false; `Startup` -> true; `Daily` -> `lastCheckUtc` null or at least 20 hours ago; `Weekly` -> null or at least 6.5 days ago (slack so a daily-started app does not skip a day/week by minutes); a `lastCheckUtc` in the future (clock change) counts as due.
- No behavior yet beyond data: the service is P12-08.

## Out of scope
- The service, view-models, UI.

## Tasks
- [ ] `UpdateCheckFrequency`, `AppSettings` members, `UpdateState`, `IUpdateStateStore`, `JsonUpdateStateStore`, `UpdateSchedule`, XML docs.
- [ ] Fix every `new AppSettings(...)` / `with` call the compiler flags (positional defaults keep most untouched).
- [ ] Tests in `tests/Squill.Platform.Tests`: old `settings.json` (without the fields) loads with defaults, new fields round trip, `UpdateSchedule.IsDue` table (all frequencies; boundaries 19h59/20h, 6.5 days; future date; null), state store round trip, missing and corrupt `update-state.json` give the empty state, writes land only in the data dir.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` 0 warnings; `rtk bypass dotnet test --solution Squill.slnx -c Release` green.
- Existing settings tests unchanged and green.

## Suggested commit message
Add update settings, persisted update state and the check schedule
