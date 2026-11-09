# P12-16 - Release workflow: tag to GitHub Release

Status: done
Depends on: P12-05, P12-01

## Goal
Pushing a tag `vX.Y.Z[-beta.N]` builds, packs and publishes the installer and the update feed to GitHub Releases, with no secret stored in the repository.

## Design decisions (final)
- `.github/workflows/release.yml` (replaces the P6-03 version that only uploaded workflow artifacts). Triggers: `push` tags `v*`, and `workflow_dispatch` with input `dry_run` (boolean, default true: build and pack, upload workflow artifacts only, create no Release). Top-level `permissions: contents: read`; the single job `windows` (`windows-latest`) sets `permissions: contents: write` (needed to create the Release). `concurrency: group: release-${{ github.ref }}`, `cancel-in-progress: false`.
- Steps, each calling a script (no logic inline, Sonar duplication rule): checkout (`fetch-depth: 0`); setup-dotnet with `global-json-file: global.json` (same actions and versions as `ci.yml`); `pwsh eng/get-version.ps1 -Tag $env:TAG` with `TAG` set from `${{ github.ref_name }}` in `env:` (fails when the tag and `eng/Version.props` disagree; for `workflow_dispatch` the tag is `v` + the file version); `dotnet build Squill.Windows.slnx -c Release -warnaserror` and `dotnet test --solution Squill.Windows.slnx -c Release` (a release never ships a red build); `pwsh eng/pack-windows.ps1 -FromGithub -AlsoBeta` for a stable version, without `-AlsoBeta` for a pre-release (the script decides from the version, so the workflow just calls it with `-FromGithub -AlsoBeta`; the script ignores `-AlsoBeta` for pre-releases, adjust P12-05's script if it does not); `eng/release-notes.ps1 -Version <v> -OutFile artifacts/release-notes.md`; upload `artifacts/releases` as workflow artifact `squill-windows` (always, also in dry run, 14 days retention); unless dry run: create the GitHub Release `v<version>` with `gh release create "$TAG" artifacts/releases/* --title "SQuilL $VERSION" --notes-file artifacts/release-notes.md` plus `--prerelease` when the version has a suffix, using `GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}` (the automatic token, not a repository secret). An existing Release for the tag makes the step fail (never overwrite, never `--clobber`).
- **Order matters for update safety**: upload the `releases.<channel>.json` files **last** (a client reads that file first; it must never list a package that is not yet downloadable). `gh release create` uploads in the order given: pass nupkgs and Setup files first, then the json files; if an upload fails the Release stays a draft: create it with `--draft`, upload everything, then `gh release edit "$TAG" --draft=false`. A failed run leaves a draft the maintainer deletes by hand (documented in `docs/release.md`).
- Version and tag come from `env:` variables, never inlined from `github.*` inside `run:`; no `pull_request_target`; the workflow is not triggered by forks.
- Signing: not here (P12-17 adds an optional conditional step).
- A small checked-in script `eng/publish-release.ps1 -Tag -Prerelease -ReleasesDir -NotesFile -DryRun` holds the `gh` calls so that the YAML stays short and the logic can be tested with `-DryRun` locally (prints the commands, runs none).

## Out of scope
- Signing, arm64, other platforms, auto-creating tags or bumping versions.

## Tasks
- [x] Workflow, `eng/publish-release.ps1`, adjust `eng/pack-windows.ps1` only if the `-AlsoBeta` rule above needs it.
- [x] Validate the YAML (`rtk bypass actionlint .github/workflows/release.yml` if installed; otherwise push the branch and run the workflow once through `workflow_dispatch` with `dry_run` true: `rtk gh workflow run release.yml --ref phase-12-release -f dry_run=true`, then `rtk gh run list --workflow release.yml --limit 1`; if the run has not finished, report "not verified" with the run URL).
- [x] Run `rtk pwsh eng/publish-release.ps1 -Tag v0.0.1-test -Prerelease -ReleasesDir artifacts/releases -NotesFile artifacts/release-notes.md -DryRun` and paste the printed command order into the report (json files last).

## Acceptance criteria
- No secret name other than `GITHUB_TOKEN` in the file; no `${{ github.* }}` inside any `run:` block (grep).
- The dry-run workflow run is green or the report states exactly which step fails and why.
- Do **not** push a `v*` tag from this card (that would create a real release); P12-19 uses local feeds, and the first real tag is the owner's decision after P12-20.

## Suggested commit message
Publish installers and the update feed to GitHub Releases from a version tag

## Report
- Added `.github/workflows/release.yml` (tag and `workflow_dispatch` with `dry_run`, job-level `contents: write`) and `eng/publish-release.ps1` (`-SelfTest` included). `eng/pack-windows.ps1` was not changed: it already ignores `-AlsoBeta` for pre-releases.
- Deviation: instead of `gh release create` with all files (gh may upload in parallel), the script runs sequential calls: create `--draft` with the notes, upload packages and installers, upload the feed files (`releases.*.json`, `assets.*.json`, `RELEASES*`), then `gh release edit --draft=false`. So the feed is guaranteed to be uploaded last.
- Dry-run output order (`-Tag v0.0.1-test -Prerelease`): `release create --draft --prerelease` -> `release upload` (nupkg, Setup) -> `release upload` (releases.*.json) -> `release edit --draft=false`.
- Only secret name: `GITHUB_TOKEN`; no `github.*` expression inside a `run:` block.
- Dry run: `workflow_dispatch` with `dry_run=true` on `phase-12-release` is green (build, test, pack, notes, artifact upload; the publish step skipped): <https://github.com/SQuilLapp/squill/actions/runs/42601295318>. Two fixes were needed on the way (`$LASTEXITCODE` is null after a script; a `using` lost in the WinUI head by the P12-15 move), as separate commits.
