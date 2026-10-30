# P4-08 - Presentation: query parameter prompt

Status: done
Depends on: P4-07

## Goal
Detect parameters in the statement to run and bind values asked from the user.

## Design decisions (final)
- `ParameterScanner.Scan(string sql, DatabaseDialect d)` (in `Squill.Presentation.Query`) returns the distinct named parameters in first-appearance order from `Parameter` tokens (`:name`, `@name`, `$name`; sigil stripped). Positional parameters (`?`, `$1`) are not supported in v1: running a statement that contains one adds a `Message` result "Positional parameters are not supported, use :name".
- `ParameterValue(string Name, string? Text, bool IsNull)`; `IDialogService` gains `ValueTask<IReadOnlyList<ParameterValue>?> PromptParametersAsync(IReadOnlyList<string> names)` (returns null on cancel; update `FakeDialogService`). Values are bound as strings, or `null` when `IsNull`; numeric coercion is left to the database. Last entered values per name are remembered for the tab's lifetime and offered as defaults.
- `QueryTabViewModel` scans each statement; when names exist it prompts once per run (not per statement) and binds `QueryParameter`s. Every provider binds `@name`, so the text sent to the provider has `:name` and `$name` rewritten to `@name` (token-based replacement, never inside strings or comments). Cancelling the prompt cancels the run.

## Out of scope
- Typed parameter editors, parameter files.

## Tasks
- [x] Implement scanner, dialog method, tab integration.
- [x] Tests: scanner per dialect (strings/comments ignored), rewriting, cancel, remembered defaults.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add query parameter detection and prompt
