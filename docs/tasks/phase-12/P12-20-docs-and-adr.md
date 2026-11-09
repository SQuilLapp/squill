# P12-20 - Documentation: release guide, ADR 0009, README and architecture note

Status: done
Depends on: P12-19, P12-17, P12-18

## Goal
Everything a maintainer needs to cut a release, and everything a user needs to install and update, is documented; the decisions are recorded.

## Design decisions (final)
- `docs/release.md` (English), sections: **Installing** (Setup.exe, where the app lives, where data lives, what SmartScreen shows while unsigned); **Updating** (the three settings, channels, delta, restart, skip, privacy: what is sent = one anonymous HTTPS request to the GitHub API per check, no identifier); **Uninstalling** (data kept in `%APPDATA%\SQuilL`, how to delete it); **Cutting a release** (edit `eng/Version.props`, fill `CHANGELOG.md`, run `rtk pwsh eng/release-notes.ps1 -Version <v>`, run the CI, tag `v<v>`, push the tag, check the Release, what a failed run leaves (a draft) and how to clean it); **Beta releases** (`-beta.N` tags); **Local build** (`eng/pack-windows.ps1` options); **Signing** (modes and secrets of P12-17, SmartScreen reputation note); **Troubleshooting** (feed override `SQUILL_UPDATE_FEED`, `SQUILL_NO_UPDATE_CHECK`, update log location per P12-01, "Not installed" status, rate limit); **Rollback** (a bad release: delete or mark pre-release the GitHub Release; users keep the installed version; publish a higher patch version — Velopack never downgrades).
- `docs/adr/0009-release-and-auto-update.md`, same format as ADR 0008: context, decision (Velopack, GitHub Releases, Windows first, channel naming and fan-out for stable tags, single version file, no automatic network call before the user chose, no update without a click, data outside the install root), alternatives considered (MSIX/App Installer, WinGet only, Squirrel.Windows, hand-written updater over `HttpClient`, ClickOnce) with the reason each was not chosen, consequences (unsigned SmartScreen friction, GitHub API rate limit, Velopack as a runtime dependency under AOT with the P12-01 evidence, arm64 outcome of P12-18), status "accepted", date.
- `README.md`: install section (download link to the latest Release, the Setup file names), update behaviour in three lines, link to `docs/release.md`; keep the existing build paragraph from P12-05.
- `docs/architecture.md`: Velopack row note "Auto-update candidate" becomes "Installer and auto-update ([ADR 0009](adr/0009-release-and-auto-update.md))"; risk table row for updates from P12-01 closed with the outcome; layer note: `Squill.Platform.Updates` and `Squill.Presentation.Updates`.
- `docs/tasks/README.md`: phase 12 section final wording and every status `done` (or `skipped` for P12-18 / P12-21 with the reason).
- `CHANGELOG.md`: the `0.1.0` section is final (no `_Nothing yet._`), date the day of the commit.

## Out of scope
- Cutting the first real release (owner's decision after reading the report of P12-19).

## Tasks
- [x] Write the four documents and the index/README edits above.
- [x] Check every link and every command quoted in `docs/release.md` by running it (`-?`/`-SelfTest` forms for scripts) or by `grep`; no command that was not tried.

## Acceptance criteria
- `rtk pwsh eng/release-notes.ps1 -Version 0.1.0` succeeds; `rtk dotnet build Squill.slnx -c Release -warnaserror` 0 warnings (docs only, nothing else changed).
- A reader following `docs/release.md` alone can cut a release: the report lists the page's section headings and the commands verified.

## Suggested commit message
Document the release process and record ADR 0009

## Report

Headings of [docs/release.md](../../release.md): Installing, Updating, Uninstalling, Cutting a release, Beta releases, Local build, Signing, Troubleshooting, Rollback.

Commands run while writing it (all succeeded): `pwsh eng/get-version.ps1` (and with `-Tag v0.1.0`), `pwsh eng/release-notes.ps1 -Version 0.1.0` and `-SelfTest`, `pwsh eng/pack-windows.ps1 -SelfTest`, `pwsh eng/publish-release.ps1 -SelfTest`, `pwsh eng/verify-update.ps1` (full run, see the P12-19 report) and `-SelfTest`, `gh release list`, `gh release delete --help` (flag `--cleanup-tag` exists), `gh workflow run --help`.

Quoted but deliberately not run: `git tag` / `git push origin <tag>` (would start a real release), `gh release delete v0.2.0`, `gh workflow run release.yml` (starts CI), the `-FromGithub` and `-Runtime win-arm64` forms of `pack-windows.ps1` (network, missing ARM64 tools). Known gap: a GitHub API rate limit shows as the generic network failure, not a dedicated message.
