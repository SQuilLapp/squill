# P12-09 - Platform: Velopack update engine (GitHub Releases or local feed)

Status: todo
Depends on: P12-01, P12-08

## Goal
The real `IUpdateEngine`: checks the GitHub Releases feed (or `SQUILL_UPDATE_FEED`), downloads, applies and restarts through Velopack, under Native AOT.

## Design decisions (final)
- **Location per the verdict of P12-01 question 9.** Default and expected: `src/Squill.Platform/Updates/VelopackUpdateEngine.cs` with `<PackageReference Include="Velopack" />` added to `Squill.Platform.csproj`. If P12-01 says Platform cannot reference Velopack without warnings, stop and report with a proposed card fix (the engine would then live in each head, behind the same interface).
- Constructor: `VelopackUpdateEngine(string repositoryUrl, string? feedOverride, RuntimeArchitecture? architecture = null)`; `AppComposition` passes `https://github.com/squillapp/squill` and `Environment.GetEnvironmentVariable("SQUILL_UPDATE_FEED")` (done in P12-11). The Velopack channel is `"<stable|beta>-<x64|arm64>"` from the requested `UpdateChannel` and `RuntimeInformation.ProcessArchitecture` (recipe section 3; arm64 is simply never published before P12-18, and a missing feed file maps to "up to date").
- Source: feed override that is an existing directory -> the local file source; an `http(s)` URL -> the simple web source; otherwise `GithubSource(repositoryUrl, accessToken: null, prerelease: channel == Beta)`. Use the type names recorded in the P12-01 report. A non-http(s), non-directory override is `UpdateFailure.FeedInvalid` (never treated as a path to create).
- Mapping: `IsInstalled`/`InstalledVersion` from `UpdateManager` (`SemanticVersion.TryParse` of its version string; null when not installed); `CheckAsync` -> `UpdateOffer` using the target release's version, markdown notes (null when empty), size and delta flag; `DownloadAsync` forwards the progress callback; `ApplyAndRestart` calls `ApplyUpdatesAndRestart` with the downloaded asset. Every Velopack/`HttpRequestException`/`IOException`/`TaskCanceledException` (not caused by the token) is mapped to `UpdateEngineException` with the closest `UpdateFailure`; unknown ones to `Unknown` with the message. Never log a URL with a token; there is none.
- Security: Velopack verifies package hashes from the feed; the engine adds nothing but refuses an offer whose version does not parse as `SemanticVersion`. No retry loops, no background threads of our own.
- No reflection, no `Activator`; if Velopack needs a JSON context for its own models under AOT, the package carries it (P12-01 evidence); do not add `rd.xml` or `NoWarn`.
- `UpdateEngineFactory.Create(...)` (public static, same namespace) returns the Velopack engine; `AppComposition` calls it in P12-11.

## Out of scope
- UI, view-models, installing anything, real GitHub calls in tests.

## Tasks
- [ ] Engine, factory, package reference (version from `Directory.Packages.props`, already 1.2.161).
- [ ] Tests in `tests/Squill.Platform.Tests/Updates/` (always run, no network): not installed -> `IsInstalled` false and `CheckAsync` throws `NotInstalled`; channel id string for the four combinations; feed override classification (directory, http URL, https URL, garbage -> `FeedInvalid`, empty -> GitHub); an unreachable `http://127.0.0.1:1/` feed maps to `Network` and `CheckAsync` honors cancellation. A check against a hand-written local feed directory (`releases.<channel>.json` listing a version) is added only if P12-01 showed it works without being installed; otherwise it is covered by P12-19 (say which in the report).
- [ ] Add the engine to the AOT proof: the WinUI and Avalonia heads must still publish with zero IL2xxx/IL3xxx warnings (this card does not reference it from the heads yet; the check is on `Squill.Platform` as a library through the heads' existing references, so run both publishes).

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` and `rtk dotnet build Squill.Windows.slnx -c Release -warnaserror` 0 warnings; `rtk bypass dotnet test --solution Squill.Windows.slnx -c Release` green.
- `rtk dotnet publish src/Squill.App.WinUI -c Release -r win-x64` and `rtk dotnet publish src/Squill.App.Avalonia -c Release -r win-x64` with zero IL2xxx/IL3xxx warnings; no process left running.

## Suggested commit message
Add the Velopack update engine over GitHub Releases or a local feed
