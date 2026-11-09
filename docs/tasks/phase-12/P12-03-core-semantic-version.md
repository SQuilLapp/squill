# P12-03 - Core: semantic version parsing and comparison

Status: done
Depends on: P12-02

## Goal
The app needs to know whether a version is newer, whether it is a pre-release, and which channel it belongs to, without depending on Velopack's types (Presentation and Core must not reference Velopack).

## Design decisions (final)
- `public readonly record struct SemanticVersion(int Major, int Minor, int Patch, string PreRelease) : IComparable<SemanticVersion>, IParsable<SemanticVersion>` in `Squill.Core` (namespace `Squill.Core`). `PreRelease` is `""` for a release. Build metadata (`+...`) is accepted by `Parse` and dropped.
- `Parse` / `TryParse` follow SemVer 2: numeric parts without leading zeros, pre-release identifiers dot-separated `[0-9A-Za-z-]+`, numeric identifiers without leading zeros; a leading `v` is **not** accepted (callers strip it). `Parse` throws `FormatException`; `TryParse` never throws. `ToString()` returns `1.2.3` or `1.2.3-beta.1`.
- Precedence exactly as SemVer 2 section 11 (a release is greater than its own pre-releases; numeric identifiers compare numerically and are lower than alphanumeric ones; a longer set of identifiers wins when all previous are equal). Implement `IComparable`, `<`, `>`, `<=`, `>=` (the compiler-synthesized equality of the record is kept; document that `CompareTo == 0` does not imply `Equals` only for build metadata, which is dropped anyway, so they agree).
- `bool IsPreRelease => PreRelease.Length > 0`.
- `UpdateChannel` enum in `Squill.Core`: `Stable`, `Beta`; `static UpdateChannel ChannelOf(this SemanticVersion v)` extension: pre-release => `Beta`, else `Stable`; `static string ChannelId(this UpdateChannel c)` => `stable` / `beta` (lowercase invariant, used in feed names; never translated).
- No allocation beyond the identifiers string; no reflection; no `System.Version`.

## Out of scope
- Anything Velopack, settings, UI.

## Tasks
- [x] `SemanticVersion`, `UpdateChannel`, extension methods with full XML docs.
- [x] Tests in `tests/Squill.Core.Tests`: the SemVer 2 precedence example chain (`1.0.0-alpha < 1.0.0-alpha.1 < 1.0.0-alpha.beta < 1.0.0-beta < 1.0.0-beta.2 < 1.0.0-beta.11 < 1.0.0-rc.1 < 1.0.0`), numeric order (`1.2.10 > 1.2.9`), parse table (valid, `v1.0.0`, `1.0`, `01.0.0`, `1.0.0-`, `1.0.0-01`, empty, `1.0.0+abc`), round trip `ToString`, `ChannelOf`, `ChannelId`, `TryParse` on garbage returns false.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` 0 warnings; `rtk bypass dotnet test --solution Squill.slnx -c Release` green.
- Branch coverage of `SemanticVersion` complete (each parse failure reason has a test).

## Suggested commit message
Add SemanticVersion and update channels to Core
