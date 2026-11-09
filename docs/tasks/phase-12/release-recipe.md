# Phase 12 - shared decisions and recipes

Used by every card of phase 12. A card points to a section; it does not copy it. These decisions are final; a card that needs to deviate stops with a report and a proposed fix.

## 1. Identity and locations

| Item | Value |
|---|---|
| Velopack pack id | `SQuilL` (fixed forever: changing it breaks updates) |
| Title / authors | `SQuilL` / `SQuilL` |
| Main exe | `Squill.App.WinUI.exe` (Native AOT, self-contained, `win-x64`; `win-arm64` only through P12-18) |
| Icon | `assets/brand/app-icons/windows/squill.ico` |
| GitHub repository | `https://github.com/squillapp/squill` (public; the app never sends a token) |
| Install root (Velopack) | `%LOCALAPPDATA%\SQuilL` (`current\` is replaced on every update) |
| User data (`AppPaths.DataDirectory`) | `%APPDATA%\SQuilL`; never written by the installer, the updater or the uninstaller. Nothing the user cares about may live under the install root |
| Local build output | `artifacts/publish` (AOT publish), `artifacts/releases` (vpk output); both git-ignored |

## 2. Versioning

- Semantic Versioning 2.0: `MAJOR.MINOR.PATCH` or `MAJOR.MINOR.PATCH-beta.N`. The first release is `0.1.0`.
- **Single source of truth**: `eng/Version.props` (property `SquillVersion`, created by P12-02), imported by `Directory.Build.props`. Release tag = `v` + that value. The release workflow fails when the tag and the file disagree; nobody types a version anywhere else. Pre-release suffix (`-beta.N`) means channel `beta` and a GitHub pre-release.
- Runtime: the generated `AppVersion` class (P12-02) gives the version string without reflection; `SemanticVersion` (Core, P12-03) parses and compares.

## 3. Channels and feed

- User-visible channels: `stable` and `beta`. Velopack channel names carry the architecture, because Velopack keeps one feed file per channel: `stable-x64`, `beta-x64` (and `stable-arm64`, `beta-arm64` after P12-18). The app builds the name from the setting plus `RuntimeInformation.ProcessArchitecture`.
- A stable tag (`v1.2.0`) is packed for **both** `stable-x64` and `beta-x64` (so beta users also receive stable releases); a beta tag (`v1.3.0-beta.1`) is packed for `beta-x64` only. Asset names carry the channel, so both sets live in the same GitHub Release.
- Feed = GitHub Releases of the repository (Velopack `GithubSource`; stable users exclude pre-releases, beta users include them). `SQUILL_UPDATE_FEED` (a directory path or an http(s) URL) replaces the feed; it exists for tests and for P12-19 and is documented, never required.
- Delta packages: the pack script downloads the previous release of the channel (`vpk download github`) before packing so `vpk pack` produces `-delta.nupkg`; when there is no previous release it packs full only (`vpk download` exits 0 even when nothing was found: test for the downloaded files, not the exit code; the Setup file name carries the channel but no version).
- Update flow in the app: check (never automatic unless the user chose a frequency) -> download in the background with progress -> "restart to update" -> `ApplyUpdatesAndRestart`. Never apply without the user's click: Velopack applies a downloaded update at the **next start** by default ([report](../../poc/velopack-aot.md) section 2), so the WinUI head calls `VelopackApp.Build().SetAutoApplyOnStartup(false)` (P12-06) and `WaitExitThenApplyUpdates` is not used.
- Privacy: no network call at startup unless the user chose `Startup`, `Daily` or `Weekly`; the first run asks once (P12-14/15). "Check now" is always available and is the only other trigger.

## 4. Commands

- Everything goes through `rtk` (CLAUDE.md). Packing: `rtk pwsh eng/pack-windows.ps1 -Version <v> -Channel stable|beta` (P12-05). A pack takes several minutes (AOT publish): run it in the background and wait for the notification.
- `vpk` is the local dotnet tool pinned in `.config/dotnet-tools.json` (`dotnet vpk ...`), never a global install. Check flags with `dotnet vpk pack --help` before using them; if a flag named in a card does not exist in the pinned version, use the closest and say so in the report.
- Installers are never run by cards before P12-19, except where a card says so. P12-19 uses a distinct pack id (`SQuilLVerify`), a throwaway `SQUILL_DATA_DIR`, and uninstalls at the end.

## 5. Strings

Every user-visible string goes through `UiText` with keys in both catalogs (parity tests of P10-17 fail otherwise). Key prefix for this phase: `update.` (banner, dialog, status, buttons), `about.` (about dialog), `settings.update.` (settings rows). Version numbers, channel ids (`stable`, `beta`) and release notes are data, not translated (channel labels are).

## 6. Quality gates

- Sonar: 0 new issues, 0 duplication on new code; put logic in `eng/*.ps1` and C#, never copy blocks between workflow jobs. In workflows never interpolate `${{ github.* }}` inside a `run:` script (put it in `env:` first) and pin third-party actions to a full commit SHA or a major tag already used in this repository.
- No secret in the repository, in logs or in a script default; optional signing reads secrets from environment variables only.
