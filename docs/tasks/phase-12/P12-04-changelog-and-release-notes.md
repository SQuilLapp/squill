# P12-04 - Changelog and release notes extraction

Status: todo
Depends on: P12-02

## Goal
Release notes are written once, in the repository, and reach both the GitHub Release body and the Velopack package (shown by the app in P12-14/15).

## Design decisions (final)
- `CHANGELOG.md` at the repository root, "Keep a Changelog" layout: a `## [Unreleased]` section, then `## [0.1.0] - 2026-11-06` style sections (`## [<SemVer>] - <yyyy-MM-dd>`), subsections `### Added`, `### Changed`, `### Fixed`, `### Removed`. Seed it with an `Unreleased` section and a `0.1.0` section summarising phases 1-11 in at most 12 bullets (read `README.md`, `docs/adr/`; features only, no commit hashes). Markdown restricted to what the app can render (P12-10): `###` headings, `-` bullets, paragraphs, `**bold**`, `` `code` `` and `[text](https://...)` links. No tables, images, HTML, nested lists.
- `eng/release-notes.ps1 -Version <semver> [-OutFile <path>]`: prints (or writes, UTF-8 without BOM, `\n` newlines) the body of the section `## [<version>]` without its heading line; exits 1 with a clear message when the section is missing or empty, or when it still contains the placeholder line `_Nothing yet._`. With `-Version Unreleased` it returns the unreleased body (used by P12-05 for local builds). It also rejects unsupported markdown (a table row starting with `|`, `<` HTML tags, `![` images) so a release never ships notes the app cannot show.
- A pre-release (`0.2.0-beta.1`) uses its own section; the script never falls back to another version.
- `docs/release.md` is written by P12-20; this card documents the format in a header comment of the script and in the first lines of `CHANGELOG.md` (HTML comment, 3 lines).

## Out of scope
- The pack script and workflow calling it (P12-05, P12-16), the notes renderer (P12-10).

## Tasks
- [ ] `CHANGELOG.md`, `eng/release-notes.ps1`.
- [ ] A Pester-free self-test: `eng/release-notes.ps1 -SelfTest` runs the script's parser over an in-script sample (good section, missing section, placeholder, table row, HTML tag) and throws on any mismatch; the script exits 0 when all pass. (No new test project; keeps the change small.)

## Acceptance criteria
- `rtk pwsh eng/release-notes.ps1 -SelfTest` exits 0.
- `rtk pwsh eng/release-notes.ps1 -Version 0.1.0` prints the seeded notes; `-Version 9.9.9` exits 1.
- No C# change; `rtk dotnet build Squill.slnx -c Release -warnaserror` still 0 warnings.

## Suggested commit message
Add CHANGELOG.md and the release notes extraction script
