# P19-03 - Stabilize the full test run

Status: todo
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
- [ ] Three full runs recorded (counts, failures). Run one of them while another `dotnet test` of a large project runs in a second shell, to reproduce the load case.
- [ ] Best-of-five in `LargeScriptAnswersQuickly`; fix any other timing test that failed.
- [ ] Final proof: three consecutive full runs, zero failures; a run under the same load, zero failures.

## Acceptance criteria
- Common acceptance (recipe 4); the Done notes carry the six run results.

## Risks
- Best-of-N hides a real regression only if every run is slow; a true O(n squared) slowdown still fails every run.
- A flaky result "fixed" by one lucky run is not proof: the six runs are required.

## Suggested commit message
Make the timing tests of the default run tolerate a loaded machine
