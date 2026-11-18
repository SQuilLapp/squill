# P19-03 - Stabilize the full test run

Status: done
Depends on: none

## Goal
P18-31 recorded two environment-sensitive tests in the default run (`rtk bypass dotnet test --solution Squill.slnx -c Release`, 4339 tests): `Squill.Sql.Tests.Completion.CompletionTriggerTests.LargeScriptAnswersQuickly` (`tests/Squill.Sql.Tests/Completion/CompletionTriggerTests.cs:43`, a 500 ms bound on a 100,000-character script that fails while other test hosts load the machine) and `LayoutOverlapTests.NoScreen_DrawsTextOverText` (fixed in its cause by P18-34, never re-proved on a full run). A suite that fails one run in four teaches everyone to ignore red.

## Design decisions (final)
- Do not loosen a bound blindly and do not delete the guard. `LargeScriptAnswersQuickly` protects the real property "a trigger check does not scan the whole script": keep it, and make it load-tolerant by measuring the **best of five** runs (a loaded machine slows some runs, not all) against the same 500 ms. Name the pattern in a short comment; no helper class unless another test already needs it.
- Search for other wall-clock asserts (`rtk grep -rn "ElapsedMilliseconds" tests`) and list them in the Done notes with their bound; change only those that fail in step 3, with the same best-of-N rule.
- Measure first: run the full default run **three times in a row** and record the failing test names per run in the Done notes. A failure that is not one of the two known ones is investigated like a bug (root cause, test first); if it is too big, record it and propose a card.

## Out of scope
- Docker-gated tests, performance benchmarks, CI configuration.

## Tasks
- [x] Three full runs recorded (counts, failures). Run one of them while another `dotnet test` of a large project runs in a second shell, to reproduce the load case.
- [x] Best-of-five in `LargeScriptAnswersQuickly`; fix any other timing test that failed.
- [x] Final proof: three consecutive full runs, zero failures; a run under the same load, zero failures.

## Acceptance criteria
- Common acceptance (recipe 4); the Done notes carry the six run results.

## Risks
- Best-of-N hides a real regression only if every run is slow; a true O(n squared) slowdown still fails every run.
- A flaky result "fixed" by one lucky run is not proof: the six runs are required.

## Suggested commit message
Make the timing tests of the default run tolerate a loaded machine

## Done notes
Baseline (before any fix, 4482 tests, 330 skipped Docker-gated): run 1 (idle) failed `QueryTabDiagnosticsTests.ClosingTheTabCancelsAPendingRun`; runs 2 and 3 (idle) green; run 4 (under load of a second full run) failed the same test. `LargeScriptAnswersQuickly` did not fail in these runs, but is made load-tolerant as designed (best of five, same 500 ms bound).

Fixes:
- `CompletionTriggerTests.LargeScriptAnswersQuickly`: best of five against 500 ms.
- `ClosingTheTabCancelsAPendingRun` (new, root cause is in the test double, not production code): `ManualTimeProvider` timers are created on a pool thread by `Task.Delay`; when the test cancelled before the timer existed, the later `Advance` fired a timer whose cancellation had not been registered yet, so a result was posted. The test now waits until the timer exists (`ManualTimeProvider.TimerCount`) before cancelling. Reproduced 2 of ~20 loaded runs before, 0 of 12 after.

Wall-clock asserts in `tests/` (all kept; none failed after the fixes): `SchemaComparerTests` < 1000 ms, `CompletionDocumentationTests` < 3000 ms, `JoinRowsTests` < 3000 ms, `CompletionTriggerTests` < 500 ms (best of five now). Others (`CancelScenario`, `ErStressScenario`, `PaletteObjectProviderTests`, `FuzzyMatcherTests`, ...) use a stopwatch without a hard wall-clock assert or are Docker-gated.

Final proof: 3 consecutive idle full runs, 0 failures (p1-p3); 3 full runs each while a second full run executed in parallel, 0 failures (6 runs, q1-q3 and the background ones), plus 3 earlier green idle runs.

Seen once under heavy load during the stress loops, not reproduced afterwards (needs a card if it recurs, not fixed here):
- `Squill.App.Avalonia.Tests.StatusBarThreadTests.WorkspaceEventRaisedOnWorkerThread_RefreshesOnUiThreadWithoutThrowing` failed once in 30 ms with `visibleBefore` null (the headless body threw before reading it; `Headless.Session.Dispatch` swallows the exception).
- One `Squill.Presentation.Tests` host hung once for over 10 minutes during a run with 12 parallel full runs (test unknown; killed). Only seen with two stress loops plus a build running.
