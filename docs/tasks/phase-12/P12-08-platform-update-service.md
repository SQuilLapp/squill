# P12-08 - Platform: update service (state machine over an engine contract)

Status: done
Depends on: P12-03, P12-07

## Goal
All update logic (when to check, state transitions, skip, errors, cancellation) in one testable class that knows nothing about Velopack: it talks to an `IUpdateEngine` (the real one comes in P12-09, tests use a fake).

## Design decisions (final)
- Namespace `Squill.Platform.Updates`.
- `IUpdateEngine`: `bool IsInstalled { get; }` (false for `dotnet run`, tests, the Avalonia head today); `SemanticVersion? InstalledVersion { get; }`; `ValueTask<UpdateOffer?> CheckAsync(UpdateChannel channel, CancellationToken ct)` (null = up to date); `ValueTask DownloadAsync(UpdateOffer offer, IProgress<int> percent, CancellationToken ct)`; `void ApplyAndRestart(UpdateOffer offer)` (does not return when it succeeds). `record UpdateOffer(SemanticVersion Version, string? ReleaseNotesMarkdown, long DownloadBytes, bool IsDelta, object Handle)` — the engine-private `Handle` is the one allowed `object` (the engine casts it back; no reflection involved; document it). Engine failures are `UpdateEngineException(UpdateFailure Kind, string Message, Exception? inner)` with `enum UpdateFailure { Network, FeedInvalid, Disk, VerificationFailed, NotInstalled, Unknown }`; the engine maps its own exceptions, the service never inspects third-party exception types.
- `UpdateStatus` enum: `Idle`, `Checking`, `UpToDate`, `Available`, `Downloading`, `Ready`, `Failed`, `NotInstalled`. `UpdateService` (sealed, `INotifyPropertyChanged`-free: raises a plain `event Action? Changed`, the view-model of P12-11 adapts) exposes `Status`, `Offer` (current `UpdateOffer?`), `Progress` (0-100), `Failure` (`UpdateFailure?` and message), `LastCheckUtc`, and methods: `ValueTask CheckNowAsync(CancellationToken)`, `ValueTask CheckIfDueAsync(CancellationToken)` (calls the schedule of P12-07 with the persisted settings and state; never checks when `IsInstalled` is false, when the frequency is `NotAsked`/`Never`, or when a check/download is running), `ValueTask DownloadAsync(CancellationToken)`, `ValueTask SkipVersionAsync()`, `void RestartToUpdate()` (only in `Ready`; otherwise `InvalidOperationException`).
- Rules: constructor takes `IUpdateEngine`, `ISettingsStore`, `IUpdateStateStore`, `TimeProvider` (default `TimeProvider.System`), and `SemanticVersion currentVersion` (from `AppVersion.Current`; used when the engine is not installed). `CheckNowAsync` with `IsInstalled` false sets `NotInstalled` and makes **no engine call** (so no network); otherwise status `Checking`, saves `LastCheckUtc` on success **and** on failure (a failing network must not retry on every start), result `UpToDate` or `Available`; an offer whose version is `<=` the installed version is ignored (`UpToDate`) — protects against downgrade; an offer equal to `SkippedVersion` is `UpToDate` for `CheckIfDueAsync` but still `Available` for `CheckNowAsync` (explicit user action clears the skip). Cancellation sets `Idle` without recording a check. Reentrancy: a second call while busy returns immediately. All state changes are made under a lock and `Changed` is raised outside it. `DownloadAsync` only from `Available`; progress reported through `Progress`/`Changed`; failure -> `Failed` with the kind, offer kept so Retry works; success -> `Ready`.
- Channel and frequency are read from the settings store at each call (no cached copy) so a change in the settings window applies to the next check.
- No `Task.Run`, no timers: the heads/Presentation call `CheckIfDueAsync` at startup; the service never polls on its own.

## Out of scope
- Velopack, UI, any real network.

## Tasks
- [x] Types above with XML docs; `FakeUpdateEngine` in `tests/Squill.Platform.Tests/Updates/` (scriptable results, call counters).
- [x] Tests: no engine call when not installed / `NotAsked` / `Never`; `Startup` checks once; `Daily`/`Weekly` due and not due with a fake `TimeProvider`; check failure still records `LastCheckUtc`; downgrade offer ignored; skipped version silent for `CheckIfDueAsync` but shown by `CheckNowAsync` and the skip cleared; cancel; reentrancy; full path Available -> Downloading (progress values) -> Ready; download failure -> Failed -> retry works; `RestartToUpdate` outside `Ready` throws; channel from settings reaches the engine; `Changed` fires once per transition and never under the lock (a handler that calls back into the service does not deadlock).

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` 0 warnings; `rtk bypass dotnet test --solution Squill.slnx -c Release` green.
- The class has no reference to `Velopack`, `HttpClient` or any UI type (`Squill.Platform.csproj` unchanged).

## Suggested commit message
Add the update service and its engine contract
