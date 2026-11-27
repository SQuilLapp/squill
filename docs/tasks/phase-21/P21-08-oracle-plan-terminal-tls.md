# P21-08 - Oracle: execution plan, sqlplus terminal and TCPS

Status: todo
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
- [ ] Parser golden tests; Docker-gated plan test in the shared scenario (P13-11).
- [ ] Terminal command-line tests.
- [ ] TCPS: unit test of the descriptor; container TCPS not attempted (record `not verified`).

## Acceptance criteria
- Recipe 2 common acceptance; `-Engine oracle` green.

## Risks
- `plan_table` may not exist on old versions: create a private one only with the user's yes; default is to show the engine error.

## Open points (defaults)
- Wallet support: profile field (default) rather than a global setting.

## Suggested commit message
Add the Oracle execution plan, the sqlplus terminal and TCPS
