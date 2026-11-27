# P21-102 - Presentation tests hang intermittently (SchemaCompareViewModelTests)

Status: done
Depends on: none

## Goal
`dotnet test tests/Squill.Presentation.Tests` hung in about 20-30 % of runs (3 of 14, 2 of 8) at 414-419 passed tests with almost no CPU; a normal run takes 12 s.

## Findings
- Method: run the test exe with `-longRunning 10`; the hung run (1 of 12) named `SchemaCompareViewModelTests.StaleScriptBuild_NeverWins` as the only test never finishing.
- Root cause (test only, the product code is correct): the test counts `Build` calls and blocks call number 1 on a gate. Both script builds run through `Task.Run` in `SchemaCompareViewModel.BuildAsync`, so the second build could reach `Interlocked.Increment` first, take number 1, and wait on the gate that only the test releases after awaiting it: deadlock.
- Fix: the test waits (bounded) until the first build has really entered `Build` before it triggers the second.
- Not the cause: `Task.Yield` in `CellPreviewViewModel.StartBackground`, xunit parallelism (the assembly is one collection).
- Other test projects: not investigated (the three Avalonia flaky tests named in the request were not touched).

## Done notes
30 consecutive runs of the Presentation test exe without a hang (see the commit message of the fix).
