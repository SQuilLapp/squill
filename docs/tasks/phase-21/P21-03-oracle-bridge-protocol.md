# P21-03 - Oracle bridge: cancel, transactions, several result sets and DBMS_OUTPUT

Status: todo
Depends on: none

## Goal
First step of making Oracle first-class: close the protocol gaps of the proof of concept ([docs/poc/oracle-bridge.md](../../poc/oracle-bridge.md), "Not supported").

Exists today: `Squill.Data.Oracle` (AOT provider, `BridgeClient`, JSON-line protocol, `BridgeJsonContext`) and `Squill.OracleBridge` (JIT, ODP.NET managed); ops `open`, `execute`, `fetch`, `closeReader`, `close`; cancelling kills the bridge; auto-commit only; one result set; no server messages.

## Design decisions (final)
- The bridge process stays (ODP.NET is not AOT compatible; re-check with the current package in the card and quote the publish output; if it has become AOT clean, stop and propose an ADR to load it in process).
- New ops: `cancel` (handled on a second reader thread of the bridge with `OracleCommand.Cancel()`, the client sends it without the request lock and the in-flight `execute`/`fetch` answers with an `ORA-01013` error), `begin` / `commit` / `rollback` / `savepoint` (`OracleTransaction`), `nextResult` (implicit results `DBMS_SQL.RETURN_RESULT` and `REF CURSOR` out parameters), `messages` (`DBMS_OUTPUT.ENABLE` at open, `GET_LINES` after each execute).
- `OracleConnection` implements `ITransactionProbe` and `IResultReader.NextResultAsync` / `Messages`; `DdlGenerators.UsesTransactionalDdl` returns false for Oracle (DDL commits implicitly; fixes the known gap of phase 20).
- `TransactionSql` gets the Oracle texts (`SET TRANSACTION`, `SAVEPOINT x`, `ROLLBACK TO SAVEPOINT x`; no `BEGIN`).

## Out of scope
- Schema objects (P21-04), SQL dialect (P21-05), plans (P21-08).

## Tasks
- [ ] Fake-bridge unit tests for each op, cancel without killing, protocol errors.
- [ ] Docker-gated tests (`-Engine oracle`): cancel a `DBMS_SESSION.SLEEP(30)` and run another statement on the same connection; manual transaction commit and rollback; two implicit results; `DBMS_OUTPUT.PUT_LINE` in the Messages tab.
- [ ] Update `docs/poc/oracle-bridge.md` (protocol table, supported list).

## Acceptance criteria
- Recipe 2 common acceptance; `-Engine oracle` green; the WinUI head still publishes Native AOT with the provider linked.

## Risks
- Two threads in the bridge writing stdout: one writer lock; the `cancel` answer has its own id.

## Open points (defaults)
- Manual transaction mode allowed on Oracle (default yes, ADR 0012 updated by P21-09).

## Suggested commit message
Add cancel, transactions, several result sets and DBMS_OUTPUT to the Oracle bridge
