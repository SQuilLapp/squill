# P21-113 - Avalonia tests hang intermittently (DesignerUiTests)

Status: todo
Depends on: none

## Goal
`dotnet test tests/Squill.App.Avalonia.Tests` hung on several consecutive runs after P21-21 and P21-22 (no output in 150 s; a normal run takes about 25 s for 204 tests). Find the root cause and fix it in the code or the test helper (no retries, no longer timeouts, no skips).

## Findings so far (not yet reproducible)
- Hung runs of the test exe (`-longRunning 10`, then `-reporter verbose`): the only running test was the first `DesignerUiTests` test (`ModifiedAndNewRows_ShowTheirTagAndDraftState`, then `ForeignKeyTab_BindsTheRows`), reached after `ImportWizardUiTests`. Every test before it finished, including `TransactionDialogTests.PasswordPrompt_...` (P21-21). The `Headless` collection is serial, so this is a hang inside `WithDesignerAsync` (connect, `window.Show`, body, `window.Close`) or in the UI thread session.
- The same pair `TransactionDialogTests` + `DesignerUiTests` hung once and then passed three times in a row. Minutes later the whole project passed: 10 sequential runs, 6 more sequential, 4 simultaneous (load test), and `dotnet test --no-build` (204 passed). No hang again, no code change in between.
- Ruled out: the new `PasswordPromptWindow` test (passes alone and with `ForeignKeyTab_BindsTheRows`, every `TransactionDialogTests` method paired with it passes), the `AvaloniaDialogService.Owner` change (the Designer path raises no dialog), CPU load (4 parallel full runs), machine-wide state in the product (no mutex, pipe or shared folder: `AppPaths` is given a temp directory).
- Suspect that remains: an external factor during the hung window (another process using the machine or a stale testhost of an earlier hung run) or a rare race in the `Session.Dispatch` helper. Needs a process dump of a hung run (`createdump` on the test exe) to name the blocked thread.

- 2026-11-27 (P21-23): no hang, but a different failure on the same project: 3 consecutive runs each failed exactly one UI test with `InvalidOperationException: The calling thread cannot access this object because a different thread owns it` (`EditBarUiTests.PreviewSql_RequestsTheGeneratedScript`, then `WindowChromeTests.StatusBar_EnvironmentBlock_ShowsEnvironmentBrush` Local and Development variants); the baseline without the P21-23 changes (stashed) failed once the same way (`ExplorerAndTabsTests.ContextMenu_OffersDefinitionAndScriptCommands_ForTheRightNodes`). A UI-thread affinity flake, unrelated to the card.

## Done notes
Open: reproduce, capture a dump, fix.
