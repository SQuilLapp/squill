# Verification report: Oracle

Card: [P21-09](../tasks/phase-21/P21-09-oracle-verification-and-docs.md). Status: **automated part done, agent drive partial, owner checklist pending**.

## 1. Automated (done)

`pwsh eng/run-docker-tests.ps1 -Engine oracle` on Podman, image `gvenzl/oracle-free:23-slim-faststart`: 114 tests, 0 skipped, all green (3 min 10 s, containers removed afterwards). It covers the bridge protocol (cancel of a running statement, manual transactions, implicit results, `DBMS_OUTPUT`), the schema reader (indexes, foreign keys, defaults, routines, triggers, view definitions), the DDL generator and designer matrix, grid editing with generated key read-back, column types, the SQL corpus, the shared plan scenario (estimated and actual) and the drop-tables scenario.

## 2. Agent drive (done where stated)

Published WinUI Native AOT exe (`eng/check-aot-warnings.ps1 -Head WinUI` and `-Head Avalonia`: only the four baseline warnings), Oracle Free 23ai container, dark theme, English, `tools/visual/Drive-Window.ps1`. Sample schema: `customers` (identity key), `orders` (foreign key, index), view, procedure.

| Step | agent |
|---|---|
| Connection saved through the editor, connect from the explorer menu and from the home card; explorer lists the schemas, `ORACLE` objects | ok |
| Run a `select`: typed columns (`Decimal`, `Varchar2`, `Date`), editable with the primary key shown | ok |
| Cancel `begin dbms_session.sleep(30); end;` | ok as designed (the server does not service a break while a session sleeps, the bridge is killed after 5 s) but the session then fails every run: bug [P21-100](../tasks/phase-21/P21-100-oracle-session-after-killed-bridge.md) |
| Manual transaction: insert counts 1 pending statement, switching back to auto-commit asks Commit / Roll back / Back, roll back | ok |
| Explain: plan tab with graph, tree, relative cost, predicate | ok |
| Analyze without `SELECT_CATALOG_ROLE` | bug found and fixed here: the generic "This engine gives no execution plan" hid the message naming the role (reason code `detail` now shows the engine text); the actual plan after the grant was not re-driven (covered by `PlanScenarioTests`) |
| ER diagram: both tables, key marks, foreign key line | ok |
| Avalonia smoke: connect from the home card, run, read-only reason shown for a result without the key | ok |
| Grid edit in the window, designer create / alter, terminal tab | not driven (context budget; covered by the Docker tests; `sqlplus` is not installed on this machine) |
| Bridge packaging: `oracle-bridge/` with `Squill.OracleBridge.exe` is in both published folders | ok |
| Missing .NET runtime: the host exits with code 150 and writes "You must install or update .NET" on stderr; the app used to say only "exited unexpectedly". It now adds that text (`BridgeExitTests`) | fixed; not re-driven in the published exe |
| TCPS against a TLS listener | not driven (no TLS listener in the container) |
| Empty password | bug [P21-101](../tasks/phase-21/P21-101-oracle-missing-password-message.md) |

Known gaps already carded or noted: wallet folder in the editors ([P21-99](../tasks/phase-21/P21-99-oracle-wallet-editor-field.md)); the parser has no `CONNECT BY` and no `DATE '...'` literal (no card: the drive did not need them).

## 3. Setup for the manual part

Start `podman run -d -p 11521:1521 -e ORACLE_PASSWORD=squill-test -e APP_USER=oracle -e APP_USER_PASSWORD=squill-test gvenzl/oracle-free:23-slim-faststart` (wait for "DATABASE IS READY TO USE!"), create a profile Oracle, host `127.0.0.1`, port 11521, database `FREEPDB1`, user `oracle`, password `squill-test`. Repeat section 3 of [sqlite.md](sqlite.md) on it (the sample tables are yours to create; `begin dbms_session.sleep(30); end;` cannot be cancelled, use a CPU-bound query).

## 4. Manual checklist (owner)

- [ ] Connections (dialog, validation fr/en, connect/reconnect, refresh)
- [ ] Query run (F5, Run All, parameters `:name`, cancel, error)
- [ ] Result grid (100k rows, copy, CSV, JSON)
- [ ] Grid editing (edit, add, delete, commit, identity key read-back)
- [ ] Manual transaction (commit, roll back, DDL warning)
- [ ] Table designer (new, alter, preview, apply)
- [ ] ER diagram, plan (Explain, Analyze after `grant select_catalog_role`)
- [ ] `sqlplus` terminal tab (needs the Oracle client)
- [ ] Avalonia smoke (connect, run, grid, edit, designer, ER)

Result of the manual part (owner): ______ (date) - rows that failed: ______
