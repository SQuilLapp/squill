# P1-02 - GitHub Actions CI workflow

Status: todo
Depends on: P1-01

## Goal
Build, test and AOT-publish on every push and pull request so each card stays verifiable off the author's machine.

## Design decisions (final)
- `.github/workflows/ci.yml`, triggers `push` and `pull_request`. Two jobs: `windows` (`windows-latest`) and `linux` (`ubuntu-latest`).
- `actions/setup-dotnet` with `global-json-file: global.json` (RC SDK, prerelease allowed by that file).
- Steps per job: `dotnet build Squill.slnx -c Release -warnaserror`, `dotnet test --solution Squill.slnx -c Release`, `dotnet publish tools/Squill.TokenGen -c Release -r <win-x64|linux-x64>`.
- Docker-gated tests stay off (`SQUILL_DOCKER_TESTS` unset). No caching, no matrix, no artifacts yet.

## Out of scope
- Release or packaging workflows (P6-03).
- Building the WinUI head (added by P5-08).

## Tasks
- [ ] Write the workflow.
- [ ] Validate YAML syntax locally (`rtk proxy python -c "import yaml,sys;yaml.safe_load(open('.github/workflows/ci.yml'))"`; if PyYAML is missing, re-read the file carefully instead).
- [ ] Run the same three commands locally for win-x64 to make sure they pass.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `ci.yml` contains the jobs `windows` and `linux`, each with build, test and publish steps.

## Suggested commit message
Add CI workflow
