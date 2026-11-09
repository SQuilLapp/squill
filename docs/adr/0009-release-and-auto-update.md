# ADR 0009 - Release, installer and automatic updates

- Status: accepted
- Date: 2026-11-09

## Context
Until phase 12 SQuilL had no way to reach a user: no installer, no version, no update. Phase 12 (cards P12-01 to P12-21, [task index](../tasks/README.md)) adds an installer, a versioned release process and an update flow that works under Native AOT and never loses user data. The shared recipe is [release-recipe.md](../tasks/phase-12/release-recipe.md); the evidence is the [Velopack report](../poc/velopack-aot.md) (P12-01) and the [end-to-end verification](../verification/release.md) (P12-19). The maintainer procedure is [docs/release.md](../release.md).

## Decision
- **Velopack** (package and `vpk` 1.2.161) is the installer, the packaging format (full and delta packages) and the update client. `Squill.Platform` references it behind `IUpdateEngine`; only the `On*FastCallback` hooks and `VelopackApp.Build()` live in the WinUI head (`Program.cs`).
- **GitHub Releases of `squillapp/squill`** are the feed (`GithubSource`; stable users exclude pre-releases, beta users include them). The app never sends a token. `SQUILL_UPDATE_FEED` (a directory or an http(s) URL) replaces the feed for tests and for the local verification.
- **Windows first.** The WinUI head is packed for `win-x64`, and for `win-arm64` on a hosted arm64 runner (P12-18). Avalonia packaging for Linux and macOS is a separate optional card (P12-21), not part of this decision.
- **Channels** are `stable` and `beta`; the Velopack channel name carries the architecture (`stable-x64`, `beta-x64`, `stable-arm64`, `beta-arm64`), because Velopack keeps one feed file per channel. A stable tag is packed for **both** `stable-x64` and `beta-x64` so beta users also receive stable releases; a `-beta.N` tag is packed for `beta-x64` only and published as a pre-release.
- **One version file**: `eng/Version.props` (`SquillVersion`, SemVer 2). The tag is `v` + that value; the workflow fails when they differ. The runtime reads it through the generated `AppVersion`, compared with `SemanticVersion` (Core).
- **No automatic network call before the user chose.** The check frequency defaults to `NotAsked`; the first run asks once; until then only *Check now* contacts the network. Privacy: one anonymous HTTPS request to the GitHub API per check.
- **No update without a click.** A download never applies by itself: the head calls `SetAutoApplyOnStartup(false)` (Velopack otherwise applies a pending update at the next start, see the report), and only *Restart to update* calls `ApplyUpdatesAndRestart`.
- **Data outside the install root.** The install root is `%LOCALAPPDATA%\SQuilL`; user data stays in `%APPDATA%\SQuilL`, which the installer, the updater and the uninstaller never write (write-path audit in P12-06, proved by the P12-19 run: seeded files byte-identical after update and uninstall).
- **Release by tag.** Pushing `v<version>` runs one workflow: build and tests, pack (x64 and arm64 through one reusable workflow), then a draft Release, uploads with the feed files last, and publication. A failed run leaves a draft, never a half-visible release. Signing is optional and secret-driven (Azure Trusted Signing or a PFX); unsigned releases work exactly the same.

## Alternatives considered
- **MSIX / App Installer.** Gives clean install and update, but needs a trusted signing certificate for every package (an unsigned MSIX does not install), a packaging project on top of the unpackaged AOT head, and App Installer update checks cannot be driven from our UI with a progress bar and "restart" button. Rejected: the signing prerequisite alone blocks a first release.
- **WinGet only.** A manifest pointing at the installer is a distribution channel, not an updater: no in-app notification, no delta, and a dependency on a community review per version. Possible later as an addition, not as the mechanism.
- **Squirrel.Windows.** The ancestor of Velopack: no longer actively developed and never built for Native AOT apps. Rejected in favour of its maintained successor, which keeps the same model.
- **Hand-written updater over `HttpClient`.** Would avoid a dependency, but means writing and owning the hard parts (file replacement while running, rollback, shortcuts, uninstall entry, delta, integrity checks). Rejected: more code, more risk, no benefit.
- **ClickOnce.** Needs .NET Framework-style deployment manifests, does not support Native AOT self-contained apps well, and its update model (checks at launch) conflicts with the "nothing before the user chose" rule. Rejected.

## Consequences
- **SmartScreen friction while unsigned.** The first releases show "Windows protected your PC"; documented in `docs/release.md`. Signing is wired and optional; reputation still takes time after signing.
- **GitHub API limit.** The unauthenticated API allows 60 requests per hour per IP. Fine for one user, possible on a shared address. The app reports every failure to reach the feed as a network failure with *Retry*; it does not distinguish a rate limit from a missing connection.
- **Velopack as a runtime dependency under AOT.** P12-01 measured it: 0 IL2xxx/IL3xxx warnings with `UpdateManager`, `GithubSource` and `SimpleFileSource`, install, delta update and uninstall from a local feed pass. The Fast hooks are Windows-only and have 15 s (update) or 30 s (install, uninstall) limits, so hooks do nothing visible. The channel in use cannot be read back from `UpdateManager` (not public), so the app keeps its own setting.
- **arm64.** This development machine lacks the MSVC ARM64 build tools, so no arm64 package was built locally (P12-01 verdict "go on CI only"); the pack script checks the PE machine type and the workflow publishes x64-only if the arm64 job fails. The first real arm64 result is the first release run; until then arm64 is untested.
- **Not proven yet.** Which release wins when several carry a channel (checked at the first real release), delta download in the verification run (the delta package is in the feed, Velopack chooses it), and the by-hand checklist of [verification/release.md](../verification/release.md).
- **Pack id is permanent.** `SQuilL` can never change: a different id breaks updates for every installed copy.
- **Velopack never downgrades.** A bad release is fixed by a higher patch version ([Rollback](../release.md#rollback)).
