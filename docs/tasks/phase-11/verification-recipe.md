# Phase 11 - shared recipes

Used by the cards of phase 11. Read the section a card points to; do not copy it into the card report.

## 1. Containers (Podman)

- Script `eng/run-docker-tests.ps1` (P11-01) starts what the tests need; never start containers by hand elsewhere. It sets `DOCKER_HOST` to the Podman pipe, `TESTCONTAINERS_RYUK_DISABLED=true` and `SQUILL_DOCKER_TESTS=1` for the child process only, and checks the Podman machine memory (SQL Server needs 4096 MiB).
- A card that needs a container states the engine: `rtk pwsh eng/run-docker-tests.ps1 -Engine postgres|mysql|sqlserver|all`.
- When Podman or the machine is not available, the card stops with a report (never marks the Docker part as verified).
- Afterwards: `podman ps -a` shows nothing started by the card, `dotnet build-server shutdown` was run.

## 2. Verification by hand (cards P11-36 ... P11-39 and every "to check by hand" line)

- A **verification report** is `docs/verification/<engine>.md`: a table `Feature | WinUI | Avalonia | Result | Note`, results `ok`, `fixed in <card or commit>`, `bug -> <new card id>`, `not driven (why)`.
- The WinUI head is driven with the window capture and input tools of P9-02 (`tools/visual`) against the published or debug exe, with `SQUILL_LANGUAGE=en`; each step that cannot be automated is listed under "to check by hand" with the exact clicks, so the owner can repeat it in two minutes.
- A bug found while verifying: fix it in the same commit when it is under ~30 lines and has a regression test; otherwise write a new card `docs/tasks/phase-11/P11-<next>-<slug>.md` (next free number, status todo) and add it to the index.

## 3. Engines and fixtures

- Images: `postgres:17-alpine`, `mysql:8.4`, `mcr.microsoft.com/mssql/server:2022-latest` (as in the existing fixtures). SQLite needs no container and its tests must never be skipped.
- Shared sample schema for end-to-end work: `tests/Shared/SampleSchema/<engine>.sql` (created by P11-02 ... P11-04: customers, orders, order_items with composite key, a view, an index, a foreign key with each referential action, a table without primary key, and one table with every supported column type; SQLite gets its own file from P11-16). Later cards reuse it, never duplicate it.
