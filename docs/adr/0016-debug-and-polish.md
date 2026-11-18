# ADR 0016 - Debug and polish (phase 19)

- Status: accepted
- Date: 2026-11-18

## Context
Phase 19 ([task index](../tasks/README.md), cards P19-01 to P19-20, shared decisions in [debug-polish-recipe.md](../tasks/phase-19/debug-polish-recipe.md)) builds no feature. Every card fixes something the repository itself recorded as broken, unverified or known-limited (`docs/verification/`, `docs/design/visual-spec.md` section 12, `shortcut:` comments, earlier done notes), or prepares and runs a verification pass. The owner's own list of issues was not available.

## Decision

### Fixes
- **Keyword quoting (P19-01)**: a name that is an SQL keyword of the dialect is quoted in completion insertions and in `PostgresPrivilegeScript` (a role named `user` no longer breaks `GRANT`). The keyword sets are the per-dialect ones of `src/Squill.Sql/Keywords`; `USER` was added to the ANSI set and the grammars in `assets/grammars` were regenerated.
- **Events on the UI thread (P19-02)**: an inventory of `src/Squill.Presentation` found **no production producer** that raises a property or collection event off the UI thread. No code changed; the head guard added by P18-34 stays.
- **Rows-cut notice (P19-04, P19-05)**: the sessions list and the privilege matrix show at most `MaxShownRows` (400) rows and now say how many are hidden (`HiddenRowCount`, `RowsCutText`). The limit itself stays a conscious `shortcut:` (no paging, no virtualization); upgrade when a real server has more sessions or objects than that.
- **Stable test run (P19-03)**: `LargeScriptAnswersQuickly` takes the best of five runs against the same 500 ms bound; `ClosingTheTabCancelsAPendingRun` waits until the fake timer exists (the cause was the test double, not production code).
- **AOT warning guard (P19-06)**: `eng/check-aot-warnings.ps1` publishes a head and fails on any `ILxxxx` warning not listed in `eng/aot-warnings.baseline.txt`. The baseline holds the four known third-party warnings (IL2104 twice for `Microsoft.Data.SqlClient`, IL2104 `System.Configuration.ConfigurationManager`, IL3053 `Microsoft.Data.SqlClient`).
- **Gaps found by the passes**: home page details line, tab count and subtitle (P19-17, P19-19); privilege matrix columns, sessions table widths, singular role members, dialog theme (P19-18); light theme brushes and the production dialog frame in WinUI (P19-20); command palette footer on Avalonia (P19-15).

### Capture tool contract (P19-07, P19-08)
`tools/visual/Capture-Window.ps1` captures the window only when it is on top at the center of its client area (otherwise exit 2, no PNG); a fully black capture exits 3; `Screen` is the supported method and `PrintWindow` a known-limited one. `New-VisualProfile.ps1 -Scenario Full` builds the reproducible data of the phase 18 screens.

### Rule for agent passes
Agent verification passes never tick the owner's boxes. They write `agent: ok`, `agent: bug -> <card>` or `agent: not driven (<why>)` in the `Result` column, and the Note says which head, theme, language and mockup were compared. Captures are not committed.

## Consequences
- A new SQuilL AOT warning fails `check-aot-warnings.ps1` instead of hiding among the third-party ones.
- The owner's checklists in `docs/verification/` are still the acceptance of the screens; the Status lines say only what the agent passes did.

## Known limits
- **Never run on Linux or macOS.** Everything in this phase ran on Windows.
- **P19-18 and P19-20 fixes were never seen in a capture.** P19-18 column widths are computed, not seen (no PostgreSQL container was running); P19-20 published and started the WinUI head but did not drive it to the workspace or the dialog, and added no test (no pattern reaches WinUI brushes).
- **P19-19** (Avalonia home) was not re-captured; P19-17 open-tab count with a live connection and the version text are covered by unit tests only.
- **P19-13 and P19-15 are partial**: WinUI grid steps 4-13, dark palette, completion popup, designer (M07), ER (M08) and shared control states were not driven; on Avalonia light theme, English, designer, completion join and snippet rows, the hops tooltip and pointer states were not driven.
- **Not driven in the window**: M09 and M10 (both heads); M09 to M12 on Avalonia; PostgreSQL `Failed` variant and plan rows; close questions, AltGr chords, narrow window; MySQL and SQL Server screens.
- **Docker and Podman scenarios**: only the PostgreSQL sessions and privileges screens of WinUI (P19-10) ran on a container. The 500-session scenario for the rows-cut notice is left to the owner.
- **Two test flakes seen once under heavy load** and not reproduced: `StatusBarThreadTests.WorkspaceEventRaisedOnWorkerThread_RefreshesOnUiThreadWithoutThrowing`, and one hung `Squill.Presentation.Tests` host.
- The Avalonia and WinUI head subscriptions were not audited one by one for off-thread events (P19-02).
- A completion popup with a single entry stayed open after `Ctrl+Enter` in WinUI once (P19-13), not investigated.
- Newer `Microsoft.Data.SqlClient` versions were not checked for trim annotations.

## Not built
- Everything listed in [ADR 0015](0015-remaining-mockups.md) "Not built" and `docs/ideas.md`.
