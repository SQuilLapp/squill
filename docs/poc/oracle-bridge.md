# Oracle through a bridge process

Problem: the app is Native AOT, `Oracle.ManagedDataAccess.Core` (ODP.NET managed) is not trim or AOT compatible (reflection, dynamic loading). It cannot be loaded in-process.

Decision (owner): host the driver in a separate, non-AOT console process, the **bridge**, and let an AOT-safe provider talk to it over stdin/stdout.

```
Squill (AOT)                                   Squill.OracleBridge (JIT, ODP.NET)
Squill.Data.Oracle                             one process per connection
  OracleDatabaseProvider --spawn--------------> Program.cs  (read line, handle, write line)
  BridgeClient  --- JSON line request  ------->   OracleSession -> OracleConnection / OracleDataReader
                <-- JSON line response -------
```

## Projects

| Project | AOT | Role |
|---|---|---|
| `src/Squill.Data.Oracle` | yes (0 IL warnings, no `NoWarn`) | provider, protocol records, JSON source-generated context, `BridgeClient`, schema reader (plain SQL over the protocol) |
| `src/Squill.OracleBridge` | **no** (only exempt project) | the exe; references the protocol of `Squill.Data.Oracle` through `InternalsVisibleTo`, so both sides share one codec |
| `tests/Squill.Data.Oracle.Tests` | n/a | unit tests with an in-process fake bridge, plus Docker-gated tests on the real bridge |

`DatabaseDialect.Oracle` is new; both heads register `OracleDatabaseProvider` in their composition root. A profile with `"dialect": "oracle"` (host, port 1521 by default, `database` = service name, default `FREEPDB1`) opens through the registry like any other engine.

## Protocol

UTF-8, **one JSON object per line**, strictly request / response in turn (the client holds a lock, one request in flight). stdout is reserved for the protocol. JSON is camelCase, nulls omitted, handled only by `BridgeJsonContext` (source generated, no reflection).

Request `{id, op, ...}`, response `{id, ok, error?, ...}`; `id` is echoed and checked. The only line sent out of turn is `cancel` (see below).

| op | request fields | response fields |
|---|---|---|
| `open` | `host`, `port`, `service`, `user`, `password`, `connectTimeoutSeconds`, `commandTimeoutSeconds` | `serverVersion` |
| `execute` | `sql`, `parameters[{name, value}]` | `columns[{name, dataType, kind}]`; `done` + `recordsAffected` when there is no result set; `messages` |
| `fetch` | `maxRows` | `rows`, `done`, `recordsAffected` (once done), `messages` (at the end of the set) |
| `nextResult` | - | `columns` of the next result set (implicit results of `DBMS_SQL.RETURN_RESULT`, `REF CURSOR`s), or `done` when there is none (the reader is then released) |
| `begin` | - | - (opens the driver's local transaction; reused when one is already open) |
| `commit` | - | - (ends the local transaction; a no-op in auto-commit) |
| `rollback` | `name?` | - (without `name` ends the transaction; with `name` rolls back to that savepoint and keeps it) |
| `savepoint` | `name` | - (needs an open transaction) |
| `probe` | - | `inTransaction`, from `DBMS_TRANSACTION.LOCAL_TRANSACTION_ID` (null until the session changed data) |
| `cancel` | - | - (interrupts the running statement, see below) |
| `closeReader` | - | - |
| `close` | - | - (the bridge exits afterwards) |

Values are typed cells `{k, v}` (kinds `s` string, `i` int64, `d` decimal, `f` double, `b` bool, `t` DateTime, `o` DateTimeOffset, `x` bytes as base64); SQL NULL is JSON `null`. Decimals travel as invariant text, so `NUMBER` keeps its precision. A value the driver cannot convert (a `NUMBER` beyond `decimal`) falls back to a string cell. The same codec encodes statement parameters. The password only travels in the `open` line, never on the command line.

Transactions: until `begin`, the driver autocommits each statement. `OracleDatabaseConnection` recognises the statements the app sends (`SET TRANSACTION READ WRITE`, `COMMIT`, `ROLLBACK`, `SAVEPOINT x`, `ROLLBACK TO [SAVEPOINT] x`, trailing `;` allowed) and runs them as the ops above, so the session code, which sends `TransactionSql` texts, works unchanged. Any other `SET TRANSACTION` form is plain SQL. DDL commits implicitly (`UsesTransactionalDdl` is false for Oracle).

Messages: the bridge runs `DBMS_OUTPUT.ENABLE(NULL)` at `open` (silently skipped without EXECUTE on the package) and drains `DBMS_OUTPUT.GET_LINE` (at most 5000 lines) after `execute`, at the end of each result set and after `nextResult`; the lines ride in the `messages` field of that response (there is no separate `messages` op) and `OracleResultReader.Messages` accumulates them.

The bridge keeps at most one open reader: `execute` closes the previous one; the reader stays open after the last row of a set so that `nextResult` can move on, and is released by `nextResult` returning `done`, `closeReader`, `close` or the next `execute`; the schema is read by the provider with ordinary `execute`/`fetch` calls over `all_objects`, `all_tab_columns`, `all_constraints` (bridge stays a generic executor). System schemas are filtered with a fixed list of Oracle users plus the connected user's own schema, valid on every version (`all_users.oracle_maintained` is absent before 12c and was found unset on a 19c server). A trailing `;` is stripped from plain statements (Oracle rejects it), but kept after `END`.

## Lifecycle

1. `OracleDatabaseProvider.OpenAsync` starts the bridge (`BridgeClient.StartProcess`) and sends `open`. Location: `SQUILL_ORACLE_BRIDGE` (an exe, or a `.dll` run with `dotnet`), else `oracle-bridge/Squill.OracleBridge(.exe)` next to the app. Missing bridge: `FileNotFoundException` naming the variable. Publishing either head (`eng/OracleBridge.targets`, imported by both) publishes the bridge into `oracle-bridge/` automatically; it is framework-dependent, so the .NET runtime must be installed.
2. One bridge process per connection (a connection = one Oracle session, same as the other engines).
3. Disposing the connection sends `close`, waits up to 2 s for the exit, then kills the process. A failed `open` stops the process too.
4. If the bridge dies or answers out of turn, the client is marked broken: later calls throw `OracleBridgeException`.
5. **Cancellation**: the bridge reads stdin on one thread and runs requests on a worker. `cancel` (negative id, so it never collides with a request id) is answered at once by the reader thread and calls `OracleCommand.Cancel()`; the in-flight `execute` / `fetch` then answers with an `ORA-01013` error, the client throws `OperationCanceledException` and the connection stays usable. The client sends `cancel` without the request lock when the token is cancelled, skips negative-id answers while reading, and only if the request is still unanswered after 5 seconds does it kill the bridge (the connection is then unusable). Both threads write stdout through one lock.

## Supported in the POC

Connect (host, port, service name, user, password, connect and command timeout), run a statement with named parameters (`:name`), stream the result set in batches, read columns (name, Oracle type name, CLR type), DML `RecordsAffected` (autocommit), cancel without killing, manual transactions with savepoints (`ITransactionProbe`), several result sets (implicit results) and `DBMS_OUTPUT` messages, list schemas (`ListDatabasesAsync` = non-Oracle-maintained users), read tables, views, columns with `NUMBER(p,s)` / `VARCHAR2(n)` types, nullability and primary key into the schema model (so the explorer shows them).

TLS ([P21-08](../tasks/phase-21/P21-08-oracle-plan-terminal-tls.md)): `TlsMode.Require` and `VerifyFull` make the provider send a `(PROTOCOL=TCPS)` descriptor in the `open` request (`descriptor`; `SSL_SERVER_DN_MATCH` is `no` / `yes`), and a profile `WalletPath` (a `TNS_ADMIN` folder) is sent as `walletPath` and set as `OracleConfiguration.TnsAdmin` and `WalletLocation`. Not verified against a TCPS listener (the container has none).

## Not supported (follow-ups)

SSH tunnel (untested), `REF CURSOR` out parameters (the next result set is read like an implicit result, but nothing declares the parameter: `QueryParameter` has no direction), indexes, foreign keys, column defaults, routines, triggers, view definitions, Oracle dialect for the SQL layer (tokenizer, completion, formatter, DDL generators fall back to the generic ANSI behaviour), CI job, bridge as a pool shared by several connections, Windows-only path of `ResolvePath` untested on Linux and macOS.

## Verification

- 59 tests (4 Docker-gated, skipped without Docker) with an in-process fake bridge (codec round trip through JSON, batching, parameters, errors, close, cancel without killing, cancel that kills a bridge that does not answer, transaction statements as ops, probe, several result sets, messages, answer with another id, schema mapping).
- 8 tests on a real `gvenzl/oracle-free:23-slim-faststart` container through the real bridge (`rtk pwsh eng/run-docker-tests.ps1 -Engine oracle`, Podman); `oracle` is not part of `all` because of the image size.
- AOT: `Squill.Data.Oracle` builds with `-warnaserror` and the AOT analyzers; the WinUI head still publishes Native AOT with the provider linked in.
