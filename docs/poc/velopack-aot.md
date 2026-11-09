# Velopack under Native AOT: report (P12-01)

Risk R9. Package `Velopack` 1.2.161 and CLI `vpk` 1.2.161 (`.config/dotnet-tools.json`), .NET SDK 11.0.100-rc.3, Windows 11 x64. Measured with a throwaway console project (`PublishAot`, `win-x64`, pack id `SQuilLPoc`), installed, updated and uninstalled; the project, the packages and the installs are deleted.

## Verdicts

| # | Question | Verdict |
|---|---|---|
| 1 | AOT publish with `UpdateManager`, `GithubSource`, `SimpleFileSource` | **Pass**: 0 IL2xxx/IL3xxx warnings |
| 2 | Check, download and apply from a directory feed, installed | **Pass** (0.0.1 -> 0.0.2 -> 0.0.3, delta used) |
| 3 | API names | Recorded below |
| 4 | Feed format, channels, delta, GitHub call | Recorded; one correction to the recipe (auto-apply on next start) |
| 5 | `vpk pack` options | Recorded; every option the phase needs exists |
| 6 | Hooks | Recorded; time limits 15 s (update) and 30 s (install, uninstall) |
| 7 | `%APPDATA%\SQuilL` untouched | **Pass**: listing identical before and after install, update and uninstall |
| 8 | arm64 | **Go on CI only** (this machine lacks the ARM64 C++ tools) |
| 9 | Adapter location | **`Squill.Platform` can reference `Velopack`** with 0 warnings |

No stop rule triggered (questions 1, 2 and 9 pass): P12-07 ... P12-15 stay as written.

## 1. AOT publish

POC: `PublishAot=true`, `RuntimeIdentifier=win-x64`, code using `VelopackApp.Build()...Run()`, `UpdateManager(IUpdateSource, UpdateOptions)`, `SimpleFileSource`, `GithubSource` (with a custom `IFileDownloader`), `CheckForUpdatesAsync`, `DownloadUpdatesAsync`, `ApplyUpdatesAndRestart`. `dotnet publish -c Release -r win-x64` from a clean tree succeeded; a `grep -E "IL[23][0-9]{3}"` over the full log gave 0 matches. The only warnings were 4 x `CA1416` in the POC (the `On*FastCallback` hooks are annotated Windows-only and the POC did not target a Windows TFM). The WinUI head targets `net11.0-windows`, so they do not apply there; a cross-platform project calling them must guard with `OperatingSystem.IsWindows()`. Published exe: 9.3 MB.

## 2. Install, check, download, apply

Packed `0.0.1`, `0.0.2` (with release notes) and `0.0.3` into one directory with `--channel stable-x64`, installed `Setup-0.0.1.exe`, ran the installed exe against the directory (`SimpleFileSource`):

- `IsInstalled=True CurrentVersion=0.0.1 AppId=SQuilLPoc`; the check returned 0.0.2 with `DeltasToTarget.Length == 1` and the markdown notes; progress callbacks `70` then `100`; `UpdatePendingRestart` was 0.0.2 after the download.
- `ApplyUpdatesAndRestart(target)` (0.0.2 -> 0.0.3): the process exited, `Update.exe apply` ran the before-update and after-update hooks, replaced `current\`, restarted the app (hook `OnRestarted`, `CurrentVersion=0.0.3`). `current\version.txt` changed from `two` to `three`.
- **Surprise (feeds the recipe, section 3)**: after a download without apply, the **next start of the app applies the pending update by itself** (`VelopackApp.Run()` auto-applies at startup: observed 0.0.1 -> 0.0.2 on a plain relaunch, then the app restarted). This is the default; `VelopackApp.Build().SetAutoApplyOnStartup(false)` turns it off.
- `Setup.exe --silent` does not start the app; `Setup.exe` without arguments starts it (hook `OnFirstRun` fired, `Main` continued with no arguments).
- Delta: the 0.0.3 delta package is 25 KB against a 6.9 MB full package.

## 3. API of 1.2.161 (copy from here)

Namespace `Velopack` unless stated. Verified by reflection and by compiling.

| Type | Member | Signature / notes |
|---|---|---|
| `UpdateManager` | constructors | `(UpdateOptions? options = null, IVelopackLocator? locator = null)`, `(string urlOrPath, UpdateOptions? = null, IVelopackLocator? = null)`, `(IUpdateSource source, UpdateOptions? = null, IVelopackLocator? = null)` |
| | `IsInstalled`, `IsPortable` | `bool` |
| | `AppId` | `string` |
| | `CurrentVersion` | `Velopack.SemanticVersion?` (`ToString()` gives `0.0.3`); null when not installed |
| | `UpdatePendingRestart` | `VelopackAsset?` (`IsUpdatePendingRestart` is obsolete) |
| | `CheckForUpdatesAsync()` | `Task<UpdateInfo?>`; null means no update (also null, with no error, when the feed has no file for the channel) |
| | `DownloadUpdatesAsync(UpdateInfo updates, Action<int>? progress = null, CancellationToken cancelToken = default)` | `Task`; progress is a percentage 0-100 |
| | `ApplyUpdatesAndRestart(VelopackAsset? toApply, string[]? restartArgs = null)` | exits the process now; `UpdateInfo` converts implicitly to `VelopackAsset`; null applies the newest downloaded |
| | `ApplyUpdatesAndExit(VelopackAsset? toApply)` | same without restart |
| | `WaitExitThenApplyUpdates(VelopackAsset? toApply, bool silent = false, bool restart = true, string[]? restartArgs = null)` | waits up to 60 s for exit; not used by the phase |
| | `Channel`, `DefaultChannel` | **not public**: the channel in use cannot be read, only set through `UpdateOptions` |
| `UpdateOptions` | `ExplicitChannel` | `string?`; null means the channel the installed package was packed with |
| | `AllowVersionDowngrade` `bool`, `MaximumDeltasBeforeFallback` `int?` | |
| `UpdateInfo` | `TargetFullRelease` | `VelopackAsset` |
| | `IsDowngrade` `bool`, `BaseRelease` `VelopackAsset?`, `DeltasToTarget` `VelopackAsset[]` | |
| `VelopackAsset` | `PackageId`, `FileName`, `SHA1`, `SHA256` | `string` |
| | `Version` | `Velopack.SemanticVersion` |
| | `Type` | `VelopackAssetType` (`Full`, `Delta`, `Portable`, `Installer`, `Msi`) |
| | `Size` | `long` |
| | `NotesMarkdown`, `NotesHTML` | `string?`; filled from `--releaseNotes`, empty for a release packed without notes |
| `Velopack.Sources` | `IUpdateSource` | `GetReleaseFeed(IVelopackLogger, string appId, string channel, Guid? stagingId, VelopackAsset? latestLocalFull)`, `DownloadReleaseEntry(IVelopackLogger, VelopackAsset, string localFile, Action<int> progress, CancellationToken)` |
| | `GithubSource` | `(string repoUrl, string? accessToken, bool prerelease, IFileDownloader? downloader = null)` |
| | `SimpleFileSource` | `(DirectoryInfo baseDirectory)` |
| | `SimpleWebSource` | `(string baseUrl, IFileDownloader? = null, double timeout = 30)` and a `Uri` overload |
| | `IFileDownloader`, `HttpClientFileDownloader` | `DownloadFile`, `DownloadBytes`, `DownloadString` |
| `VelopackApp` | builder | `Build()`, `SetAutoApplyOnStartup(bool)`, `SetArgs`, `SetLocator`, `SetLogger`, `SetAppUserModelId`, `Run()` |
| | hooks | `OnFirstRun`, `OnRestarted`, `OnAfterInstallFastCallback`, `OnBeforeUpdateFastCallback`, `OnAfterUpdateFastCallback`, `OnBeforeUninstallFastCallback`; delegate `VelopackHook(SemanticVersion version)` |

## 4. Feed format, channels, delta, GitHub

- `vpk pack --channel stable-x64 --packVersion 0.0.2 -o rel` produced `SQuilLPoc-0.0.2-stable-x64-full.nupkg`, `SQuilLPoc-0.0.2-stable-x64-delta.nupkg` (only when the previous full package of the **same channel** is in the output directory), `SQuilLPoc-stable-x64-Setup.exe`, `SQuilLPoc-stable-x64-Portable.zip`, `releases.stable-x64.json` (the feed: every asset with SHA1, SHA256, size, notes markdown and HTML), `RELEASES-stable-x64` (legacy) and `assets.stable-x64.json`. The **Setup name carries no version**, only the channel: one GitHub Release holds one Setup per channel.
- Stable and beta in one directory (recipe section 3): packing `0.0.4` for `stable-x64` then for `beta-x64` into the same directory produced two disjoint file sets (`releases.beta-x64.json`, `...-beta-x64-Setup.exe`, ...). A channel with no earlier release in the directory gets **no delta** (the `beta-x64` pack of `0.0.4` had none; `0.0.5-beta.1` then got one against `0.0.4`). The pack script must therefore run `vpk download github --channel <ch> [--pre]` for each channel it packs.
- `vpk download github --repoUrl https://github.com/squillapp/squill --channel stable-x64` on the repository (no release yet): warning `No releases found`, **exit code 0**, nothing downloaded. The script must test for the downloaded files, not the exit code.
- Installed `stable-x64` with `ExplicitChannel = "beta-x64"` against a directory holding only stable files: `CheckForUpdatesAsync` returned null, no exception.
- `GithubSource` makes one anonymous call per check: `GET https://api.github.com/repos/squillapp/squill/releases?per_page=10&page=1` with only an `Accept` header (observed through a logging `IFileDownloader`), then downloads `releases.<channel>.json` and the packages. The package documents the unauthenticated limit as 60 requests per hour per IP: fine for one user, but a shared office IP can hit it, so the UI must present a rate-limit failure as "try again later" (P12-08). `prerelease=false` ignores pre-releases, `true` includes them. **Not verified** against real releases (the repository has none): which release is chosen when several carry the channel is to be checked in P12-19 and at the first real release.
- The channel naming of recipe section 3 works as written: the installed package carries its channel, `ExplicitChannel` switches it.
- Install side effects: `%LOCALAPPDATA%\<packId>\{current, packages, Update.exe, <Title>.exe stub}`, a Start Menu shortcut, an uninstall registry key, and a log file `%LOCALAPPDATA%\velopack\velopack_<packId>.log` (outside the install root, left behind by the uninstaller). `packages\.betaId` is a GUID file for staged rollouts (no network use observed).

## 5. `vpk pack` options (1.2.161)

`--packId/-u`, `--packVersion/-v`, `--packDir/-p`, `--mainExe/-e`, `--outputDir/-o`, `--channel/-c`, `--runtime/-r`, `--packTitle`, `--packAuthors`, `--releaseNotes <file>`, `--icon/-i`, `--shortcuts` (default `Desktop,StartMenuRoot`), `--delta <DeltaMode>` (default `BestSpeed`; `None`, `BestSize` and `BestSpeed` parse), `--framework/-f` (runtime prerequisites: **not needed**, the exe is self-contained AOT and the POC installed with no prerequisite), `--noPortable`, `--noInst`, `--exclude` (default `.*\.pdb`), `--splashImage`, `--aumid`, `--msi`, `--skipVeloAppCheck`. Signing: `--signParams/-n` (signtool parameters), `--signTemplate` (custom command with `{{file}}`), `--azureTrustedSignFile` (Azure Trusted Signing `metadata.json`), `--signExclude`, `--signParallel`. Every option can also be given as an environment variable `VPK_*` (for example `VPK_CHANNEL`). Without signing parameters `vpk` prints `No signing parameters provided` and continues. `vpk download github`: `--repoUrl` (required), `--channel`, `--pre`, `--token`, `--outputDir`. `vpk pack` verifies that the app calls `VelopackApp.Build().Run()` (hence `--skipVeloAppCheck`).

## 6. Hooks

All run as a **separate short-lived process** of the app exe with a `--veloapp-*` argument, before the rest of `Main`:

| Hook | Observed argument | Behaviour |
|---|---|---|
| `OnAfterInstallFastCallback` | `--veloapp-install 0.0.1` | the hook runs, then the process **exits** (code after `Run()` is not reached); limit 30 s |
| `OnFirstRun` | none (a normal launch right after install) | fires only when the app is started by `Setup.exe` (not with `--silent`); `Main` **continues** |
| `OnBeforeUpdateFastCallback` | `--veloapp-obsolete 0.0.2` (the old version) | runs in the old exe, then exits; limit 15 s |
| `OnAfterUpdateFastCallback` | `--veloapp-updated 0.0.3` (the new version) | runs in the new exe, then exits; limit 15 s |
| `OnRestarted` | the restart arguments | after `ApplyUpdatesAndRestart`; `Main` continues |
| `OnBeforeUninstallFastCallback` | `--veloapp-uninstall 0.0.3` | runs, then exits; limit 30 s |

`Update.exe` kills a hook after 15 s (update) or 30 s (install, uninstall): hooks must not use the network or the UI. The Fast hooks are Windows-only (`CA1416` elsewhere).

## 7. Data directory

A snapshot of every file under `%APPDATA%\SQuilL` (path, size, last write time; the real data of this machine) taken before the POC and compared after install, two updates and uninstall: **0 differences**. `%APPDATA%\SQuilLPoc` never existed. Velopack only used `%LOCALAPPDATA%\SQuilLPoc` (removed by the uninstaller: gone a few seconds after `Update.exe uninstall --silent`), the shortcut and the registry key (both removed) and its log file.

## 8. arm64

`dotnet publish src/Squill.App.WinUI -c Release -r win-arm64` on this machine: the managed build succeeded, the ILCompiler step failed with `vswhere.exe failed to locate Visual Studio with Microsoft.VisualStudio.Component.VC.Tools.ARM64`. Missing prerequisite: the Visual Studio component "MSVC v143 C++ ARM64 build tools (Latest)". It is not installed here and this card does not change the machine. GitHub's hosted `windows-11-arm` runner is the natural place; it was **not tested**. Verdict for P12-18: **go-on-CI-only**; the first arm64 build happens in the workflow, and a failure there must leave the release x64-only.

## 9. Adapter location

A library project under the repository's `Directory.Build.props` (`IsAotCompatible`, trim and AOT analyzers, `TreatWarningsAsErrors`, `AnalysisLevel latest-recommended`) referencing `Velopack` (central version) and calling `new UpdateManager(new GithubSource(...), new UpdateOptions { ExplicitChannel = ... })` then `CheckForUpdatesAsync`: `dotnet build -c Release` gave 0 warnings, 0 errors (a first CA1822 error was only the probe method needing `static`). Verdict: **`Squill.Platform` may reference `Velopack`**, so the engine of P12-09 lives there. Only the `On*FastCallback` hooks need a Windows guard and stay in the WinUI head (`Program.cs`, P12-06).

## Effects on the recipe

`release-recipe.md` section 3 said a downloaded, unapplied update is applied on a normal exit; it is applied at the **next start** (default `SetAutoApplyOnStartup(true)`). The recipe now requires `SetAutoApplyOnStartup(false)` in the head's `Program.cs` (P12-06), so nothing is applied without the user's click. The recipe also records that `vpk download` exits 0 when nothing was found and that the Setup name has no version.
