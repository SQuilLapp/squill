# Releasing and updating SQuilL

How users install, update and uninstall SQuilL on Windows, and how a maintainer cuts a release. The decisions behind it are in [ADR 0009](adr/0009-release-and-auto-update.md); the shared recipe of the phase is [release-recipe.md](tasks/phase-12/release-recipe.md). All commands below are PowerShell 7 (`pwsh`) from the repository root.

## Installing

1. Download `SQuilL-stable-x64-Setup.exe` (`SQuilL-stable-arm64-Setup.exe` on Windows on ARM, when the release has it) from the [latest release](https://github.com/squillapp/squill/releases/latest) and run it. There is no prompt: it installs for the current user (no administrator rights) and starts the app. Beta testers download `SQuilL-beta-x64-Setup.exe` from a pre-release.
2. The app lives in `%LOCALAPPDATA%\SQuilL` (`current\` is replaced on every update). A Start menu and a desktop shortcut are created.
3. Your data (profiles, encrypted secrets, history, snippets, settings, diagram layouts) lives in `%APPDATA%\SQuilL`, never under the install folder. The installer, the updater and the uninstaller never write there.
4. While the installer is **unsigned**, Windows SmartScreen shows "Windows protected your PC": choose *More info*, then *Run anyway*. The warning disappears with a signed installer and an established reputation (see Signing).

## Updating

- **Settings > Updates**: *Check for updates* (Never, At every start, Every day, Every week) and *Update channel* (Stable, Beta). The first run asks once; until you answer, nothing is checked and no network call is made.
- *Help > About > Check now* is always available and is the only other trigger.
- When a newer version exists a banner appears: *Details* shows the notes and the download size, *Download* fetches it in the background with a progress bar (only the changes when a delta package exists), then *Restart to update* applies it. Nothing is applied without that click, and a downloaded update that was not applied stays pending. *Skip this version* hides one version; *Later* hides the banner until the next check.
- Channels: **Stable** follows stable releases. **Beta** follows beta and stable releases, so a beta user also gets the next stable. Switching back to Stable never goes back to an older version: you get the next stable release.
- Privacy: each check sends **one anonymous HTTPS request** to the GitHub API (`api.github.com`, the releases of `squillapp/squill`) and then downloads the feed file and the packages from GitHub. No identifier, no token, no telemetry.
- A copy that is not installed through the installer (for example a build from source) says "Not installed" in About and does not update itself.

## Uninstalling

Use Windows *Settings > Apps > Installed apps > SQuilL > Uninstall*. The install folder and the shortcuts are removed. **Your data stays** in `%APPDATA%\SQuilL` by design; to remove it too, delete that folder by hand after the uninstall. Velopack also leaves a log file, `%LOCALAPPDATA%\velopack\velopack_SQuilL.log`.

## Cutting a release

Prerequisite: the `main` branch is green and nothing in the repository holds a real secret.

1. Edit `eng/Version.props` (`SquillVersion`, SemVer 2, for example `0.2.0`). It is the only place a version is written.
2. Fill `CHANGELOG.md`: move the `## [Unreleased]` notes into a new `## [0.2.0] - yyyy-MM-dd` section (`### Added`, `### Changed`, `### Fixed`, `### Removed`, `-` bullets only; the app renders these notes). Leave `## [Unreleased]` with `_Nothing yet._`.
3. Check the notes and the version file:
   ```powershell
   pwsh eng/release-notes.ps1 -Version 0.2.0
   pwsh eng/get-version.ps1
   ```
   `release-notes.ps1` fails when the section is missing, empty or still the placeholder.
4. Commit and push, then run the CI on that commit. The `Release` workflow can also be started by hand without publishing anything (`gh workflow run release.yml`, `dry_run` defaults to true): it builds, tests and packs and uploads workflow artifacts only.
5. Tag and push the tag. This starts the real release:
   ```powershell
   git tag v0.2.0
   git push origin v0.2.0
   ```
   The tag must be exactly `v` + the version of `eng/Version.props`; the workflow fails otherwise (`pwsh eng/get-version.ps1 -Tag v0.2.0` runs the same check).
6. The workflow packs x64 (build and tests first) and arm64, then creates the GitHub Release as a **draft**, uploads the installers and packages, uploads the feed files (`releases.<channel>.json`) last, and only then publishes it. Check the [Release page](https://github.com/squillapp/squill/releases): the notes, `SQuilL-stable-x64-Setup.exe`, the `*-full.nupkg` files, a `*-delta.nupkg` (from the second release on) and the `releases.*.json` files must be there.
7. **A failed run leaves a draft**, never a half-visible release (users read the feed only after publication). Delete it by hand and fix the cause, then tag again:
   ```powershell
   gh release list
   gh release delete v0.2.0 --cleanup-tag
   ```
   `--cleanup-tag` also deletes the tag. The script never overwrites an existing Release for a tag.

A failed arm64 job does not block the release: it is published x64-only. A failed x64 job blocks it.

## Beta releases

Use a pre-release version, for example `0.3.0-beta.1`, in `eng/Version.props`, a `## [0.3.0-beta.1] - yyyy-MM-dd` section in `CHANGELOG.md` (a pre-release has its own section, there is no fallback), and the tag `v0.3.0-beta.1`. The release is packed for the `beta-x64` channel only and marked as a GitHub pre-release; stable users never see it. A stable tag (`v0.3.0`) is packed for **both** `stable-x64` and `beta-x64`, so beta users also receive stable releases. Velopack channel names carry the architecture (`stable-x64`, `beta-x64`, and `-arm64` for the arm64 packages; off Windows the OS too, for example `stable-linux-x64`).

## Local build

```powershell
pwsh eng/pack-windows.ps1                                          # version of eng/Version.props, needs its CHANGELOG section
pwsh eng/pack-windows.ps1 -Version 0.0.1-test -AllowVersionOverride  # local check, falls back to the Unreleased notes
pwsh eng/pack-windows.ps1 -FromGithub -AlsoBeta                    # downloads the previous release so vpk builds deltas; also packs beta-x64
pwsh eng/pack-windows.ps1 -Runtime win-arm64                       # needs the MSVC ARM64 build tools
pwsh eng/pack-windows.ps1 -SelfTest                                # checks the output checks, packs nothing
```

The output is `artifacts/releases` (installers, packages, feed files; git-ignored), the publish folder is `artifacts/publish`. A pack takes several minutes (Native AOT publish). `dotnet tool restore` installs the pinned `vpk`. To prove the whole chain on one machine (install, update through a local feed, uninstall, data kept) run `pwsh eng/verify-update.ps1`; the last run is recorded in [verification/release.md](verification/release.md).

## Linux and macOS (Avalonia)

The Avalonia head is packed by `eng/pack-unix.ps1` (job `pack-unix` of the Release workflow, on `ubuntu-latest` and `macos-latest`; each runtime can only be packed on its own OS, never from Windows):

- **Linux x64**: `vpk pack` builds an AppImage plus the update feed for the channels `stable-linux-x64` (and `beta-linux-x64` for a published stable release). The OS is part of the channel name so the files never collide with the Windows ones in the same Release. No delta packages yet.
- **macOS arm64**: `vpk bundle` builds the `.app`, which the script zips as `SQuilL-<version>-osx-arm64.zip`. There is no feed, so no automatic update on macOS: users download the next zip.
- **Ad hoc signed only, not notarized**: the `.app` gets a certificate-less signature (Apple Silicon refuses unsigned code) but Gatekeeper still warns. A downloaded copy may say "is damaged": run `xattr -cr SQuilL.app` to remove the quarantine flag. Linux needs `chmod +x` on the AppImage.
- **Published, not run**: nobody has run these packages on a real Linux or macOS desktop. A failed job never blocks the Windows release; check the artifacts by hand before announcing them.

`pwsh eng/pack-unix.ps1 -Runtime linux-x64` (or `osx-arm64`) repeats the pack on the matching OS; the output is `artifacts/releases-<runtime>`.

## Signing

Signing is optional and off unless `-Sign` is given (the release workflow turns it on by itself when a signing secret exists). Without it the installer and the app are unsigned exactly as before. Two modes, read from environment variables only (never from a file or a default):

- **Azure Trusted Signing**: `AZURE_TRUSTED_SIGNING_METADATA_FILE` (path of the `metadata.json` with endpoint, account and profile); credentials come from the Azure login. In CI the secrets are `AZURE_TRUSTED_SIGNING_ENDPOINT`, `AZURE_TRUSTED_SIGNING_ACCOUNT`, `AZURE_TRUSTED_SIGNING_PROFILE`, `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` (federated identity, no client secret).
- **PFX certificate**: `SQUILL_SIGN_PFX_BASE64` (base64 of the `.pfx`) and `SQUILL_SIGN_PFX_PASSWORD`; optional `SQUILL_SIGN_TIMESTAMP_URL` (default `http://timestamp.digicert.com`).

After the pack, the app executable and every Setup file must have an Authenticode status of `Valid`. A signed file does not remove the SmartScreen warning immediately: reputation builds up with downloads (an Azure Trusted Signing identity gets it faster than a plain certificate). Secrets are never printed and are not available to pull requests or forks.

## Troubleshooting

- **Use another feed**: `SQUILL_UPDATE_FEED` set to a directory or an `http(s)` URL replaces the GitHub feed (used by `eng/verify-update.ps1`).
- **No update check at start**: `SQUILL_NO_UPDATE_CHECK=1` skips the scheduled check and the one-time question (a debug aid; "Check now" still works).
- **Other test switches**: `SQUILL_DATA_DIR` moves the data directory, `SQUILL_LANGUAGE=fr|en|system` overrides the language for one run.
- **Log**: Velopack writes `%LOCALAPPDATA%\velopack\velopack_SQuilL.log` (install, update, uninstall steps); it survives an uninstall.
- **"Not installed"** in About and a disabled update: the app was not started from an installer-made folder (a build from source, or a copy of `current\`). Install with `Setup.exe`.
- **"GitHub could not be reached"**: no network, a proxy, or the unauthenticated GitHub API limit (60 requests per hour per IP; an office behind one address can reach it). The app shows the same message for all three; wait and press *Retry*.
- **The installer is blocked**: see Installing, step 4.

## Rollback

A bad release is handled on GitHub, never on users' machines: delete the Release (or edit it and tick *pre-release*, which hides it from stable users) with `gh release delete <tag>` or on the Release page. Users who already updated keep the installed version: **Velopack never downgrades**. Fix the problem and publish a higher patch version (for example `0.2.1`); everyone, including those on the bad version, updates to it. Do not reuse a tag or a version number.
