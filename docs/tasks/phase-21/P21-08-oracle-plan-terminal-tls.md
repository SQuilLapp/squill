# P21-08 - Oracle: execution plan, sqlplus terminal and TCPS

Status: done
Depends on: P21-03

## Goal
`Expliquer` on Oracle, the native terminal client, and an encrypted connection.

Exists today: `IPlanReader` for four engines; `TerminalLauncher` / `TerminalCommandLine` for `psql`, `mysql`, `sqlcmd`, `sqlite3` with the password in the environment only; `TlsMode` mapped by four providers; the Oracle POC has no TLS and no terminal.

## Design decisions (final)
- Plan: `EXPLAIN PLAN SET STATEMENT_ID = '<guid>' FOR <stmt>` then `SELECT ... FROM plan_table WHERE statement_id = ...` (tree by `id` / `parent_id`, `cost`, `cardinality`); a pure `OraclePlanParser`. `Analyser` (actual rows) only with `/*+ GATHER_PLAN_STATISTICS */` and `DBMS_XPLAN.DISPLAY_CURSOR(format => 'ALLSTATS LAST')` for read-only statements; if the user lacks `V$` access, `Analyser` is disabled with the engine message.
- Terminal: `sqlplus -L user@//host:port/service`; the password through `/nolog` + `CONNECT` piped on stdin is refused (it would sit in the process input); instead the launch is allowed only with a wallet or when the user types the password in the terminal. Default: the user types it.
- TLS: `TlsMode.Require`/`VerifyFull` map to a `(PROTOCOL=TCPS)` descriptor sent in the `open` op; wallets (`TNS_ADMIN` folder) as an optional profile field passed to the bridge environment.

## Out of scope
- Oracle in the sessions monitor and the schema compare (later card if wanted).

## Tasks
- [x] Parser golden tests; Docker-gated plan test in the shared scenario (P13-11).
- [x] Terminal command-line tests.
- [x] TCPS: unit test of the descriptor; container TCPS not attempted (record `not verified`).

## Acceptance criteria
- Recipe 2 common acceptance; `-Engine oracle` green.

## Risks
- `plan_table` may not exist on old versions: create a private one only with the user's yes; default is to show the engine error.

## Open points (defaults)
- Wallet support: profile field (default) rather than a global setting.

## Suggested commit message
Add the Oracle execution plan, the sqlplus terminal and TCPS

## Done notes
- Premise check: the plan was already there. `OracleDatabaseConnection` implements `IPlanReader` (estimated through `EXPLAIN PLAN` + `PLAN_TABLE` + `DBMS_XPLAN`, actual through `alter session set statistics_level = all` and `V$SQL_PLAN_STATISTICS_ALL`, `PlanUnavailableException` with the engine message when `V$` is not readable) and `OraclePlanParser` with `OraclePlanParserTests` existed (issue 33). What was missing, and added here: the Oracle run of the shared `PlanScenario` (`PlanSpec.FetchFirst` for `fetch first 5 rows only`), terminal, TCPS and wallet.
- `TerminalLauncher` had no Oracle branch (it threw `ArgumentOutOfRangeException`). Now `sqlplus -L user@//host:port/service` (`tcps://` when TLS is Require or VerifyFull); no password is passed in any form, the user types it in the terminal. A wallet profile signs in with `-L /@...` and sets `TNS_ADMIN`. New text `terminal.install.sqlplus` in both catalogs.
- `ConnectionProfile.WalletPath` (optional, persisted by the existing source-generated context) and `ConnectionEditorViewModel.WalletPath` so editing a profile keeps it. No field in the two connection editors (heads untouched, so no AOT or drive step): new card P21-99.
- TCPS: `OracleDescriptor.Build` gives `(DESCRIPTION=(ADDRESS=(PROTOCOL=TCPS)...)(SECURITY=(SSL_SERVER_DN_MATCH=no|yes)))`; `BridgeRequest.Descriptor` replaces host/port/service as the data source and `WalletPath` sets `OracleConfiguration.TnsAdmin` / `WalletLocation` in the bridge (sent in the `open` request instead of the process environment: same effect, one process per connection). Unit-tested through the fake bridge. Not verified: no TCPS listener in the container, and ODP.NET still validates the server certificate chain (trusted store or wallet) even for `Require`.
- New card P21-99: wallet folder field (and a note that `Require` still needs a trusted certificate) in the WinUI and Avalonia connection editors for Oracle, with the head checks of recipe 3.
- Bug found by the new Docker test and fixed: the actual plan read the cursor of `V$SESSION.PREV_SQL_ID`, but the bridge runs `DBMS_OUTPUT.GET_LINE` after every statement, so that cursor was never the user's (no rows). The statement now runs with a leading `/* SQ<guid> */` comment and its cursor is found in `V$SQL` by that marker.
- Docker (Oracle Free, Podman): `PlanScenarioTests` runs the shared `PlanScenario` (three statements estimated and actual, write refused by `PlanEligibility`, cancel of a slow actual run) after asserting that the application user, without `SELECT_CATALOG_ROLE`, gets a `PlanUnavailableException` naming the role while the estimated plan still works; the test then grants the role as `SYSTEM` and reconnects. `-Engine oracle`: 114 of 114 green. Full solution run: only failures were the string scanner (fixed: a literal in `TerminalLauncher`) and `EditBarUiTests.AddRow_AddsAPendingRowAndTheIssueDisablesCommit` (Avalonia, passed on rerun, flaky).
- The support matrix row of ADR 0008 is left to P21-09 (a paragraph was added under the matrix).
