# P11-05 - CI runs the Docker-gated tests on Linux

Status: done
Depends on: P11-02, P11-03, P11-04

## Goal
Once green locally, the container tests also run on every push so that an engine regression is caught without the owner's machine.

## Design decisions (final)
- In `.github/workflows/ci.yml`, job `linux` (ubuntu-latest has Docker): add a step with `SQUILL_DOCKER_TESTS: 1` that runs the three driver test projects (`tests/Squill.Data.{Postgres,MySql,SqlServer}.Tests`); keep the existing full-solution step with the gate off. Ryuk stays enabled on CI (do not set `TESTCONTAINERS_RYUK_DISABLED`).
- Pull the three images in a prior step (`docker pull`) so that image downloads do not count against test timeouts. No memory tuning for SQL Server (the runners are large enough).
- No matrix, no caching. A test that is flaky on CI gets its cause fixed (wait strategy), never retries.
- `docs/testing.md` gets one paragraph about the CI job.

## Out of scope
- Windows CI containers, nightly schedules.

## Tasks
- [x] Edit the workflow, push, then check `rtk gh run list --branch phase-11-engine-parity --limit 1` once; if the run has not finished, report "not verified" with the run URL instead of waiting.

## Acceptance criteria
- The workflow YAML is valid (the run starts).
- The `linux` job is green, or the report states exactly which step fails and why.

## Suggested commit message
Run the Docker-gated driver tests in the Linux CI job
