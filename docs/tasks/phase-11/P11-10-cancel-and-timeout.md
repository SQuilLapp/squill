# P11-10 - Cancel and timeout proven on every engine

Status: done
Depends on: P11-07

## Goal
Cancelling a long statement really stops it on the server and leaves the connection usable, and a command timeout produces a clear message, on each engine; today `QueryTabViewModel` cancels through a token but no test ever ran a slow statement against a driver.

## Design decisions (final)
- Data-level tests (one class `CancelAndTimeoutTests` per driver test project): a slow statement per dialect (`select pg_sleep(30)`, `select sleep(30)`, `waitfor delay '00:00:30'`, SQLite recursive CTE counting to a huge number), started through `IDatabaseConnection.ExecuteAsync`, cancelled after 500 ms; assert: `OperationCanceledException` (or the driver's cancellation exception wrapped; if a driver throws something else, `PostgresDatabaseConnection` etc. convert it to `OperationCanceledException` when the token is cancelled) within 5 s, then `select 1` on the same connection works. Second test: `CommandTimeoutSeconds = 1` and the same slow statement -> the driver timeout exception within 5 s, then `select 1` works. SQLite test runs everywhere; the others are Docker-gated.
- Cancel while reading rows (`ReadBatchAsync` on a large generated result: `generate_series(1, 50000000)`, MySQL recursive CTE with `cte_max_recursion_depth`, SQL Server cross join of `sys.objects`, SQLite recursive CTE): cancel after the first batch, assert the reader disposes in under 5 s (drivers drain the rest on dispose; if one does, add `command.Cancel()` before dispose in that reader).
- Presentation test with `FakeDatabaseProvider`: a timeout exception from the connection sets the status to an error message that contains the driver message and re-enables Run (look at `QueryTabViewModel.ExecuteAsync`; fix if the status stays "running").

## Out of scope
- UI changes, a settings screen for the timeout (it is per profile, P11-08).

## Tasks
- [x] Tests above; fix what they find in the four connections/readers or `QueryTabViewModel`.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` 0 warnings; default test run green; `rtk pwsh eng/run-docker-tests.ps1 -Engine all` green; no container left.

## Suggested commit message
Prove cancel and command timeout on every engine
