# Oracle through a bridge process (proof of concept)

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

Request `{id, op, ...}`, response `{id, ok, error?, ...}`; `id` is echoed and checked.

| op | request fields | response fields |
|---|---|---|
| `open` | `host`, `port`, `service`, `user`, `password`, `connectTimeoutSeconds`, `commandTimeoutSeconds` | `serverVersion` |
| `execute` | `sql`, `parameters[{name, value}]` | `columns[{name, dataType, kind}]`; `done` + `recordsAffected` when there is no result set |
| `fetch` | `maxRows` | `rows`, `done`, `recordsAffected` (once done) |
| `closeReader` | - | - |
| `close` | - | - (the bridge exits afterwards) |

Values are typed cells `{k, v}` (kinds `s` string, `i` int64, `d` decimal, `f` double, `b` bool, `t` DateTime, `o` DateTimeOffset, `x` bytes as base64); SQL NULL is JSON `null`. Decimals travel as invariant text, so `NUMBER` keeps its precision. A value the driver cannot convert (a `NUMBER` beyond `decimal`) falls back to a string cell. The same codec encodes statement parameters. The password only travels in the `open` line, never on the command line.

The bridge keeps at most one open reader: `execute` closes the previous one; the schema is read by the provider with ordinary `execute`/`fetch` calls over `all_objects`, `all_tab_columns`, `all_constraints` (bridge stays a generic executor). The provider reads the major version from `serverVersion` and adapts the catalog queries: from 12c the system schemas are filtered with `all_users.oracle_maintained`, before (11g, XE) with a fixed list of system users. A trailing `;` is stripped from plain statements (Oracle rejects it), but kept after `END`.

## Lifecycle

1. `OracleDatabaseProvider.OpenAsync` starts the bridge (`BridgeClient.StartProcess`) and sends `open`. Location: `SQUILL_ORACLE_BRIDGE` (an exe, or a `.dll` run with `dotnet`), else `oracle-bridge/Squill.OracleBridge(.exe)` next to the app. Missing bridge: `FileNotFoundException` naming the variable. Publishing either head (`eng/OracleBridge.targets`, imported by both) publishes the bridge into `oracle-bridge/` automatically; it is framework-dependent, so the .NET runtime must be installed.
2. One bridge process per connection (a connection = one Oracle session, same as the other engines).
3. Disposing the connection sends `close`, waits up to 2 s for the exit, then kills the process. A failed `open` stops the process too.
4. If the bridge dies or answers out of turn, the client is marked broken: later calls throw `OracleBridgeException`.
5. **Cancellation**: a running statement cannot be interrupted. Cancelling the token kills the bridge (so the Oracle session ends and the statement stops) and the connection is unusable afterwards. Acceptable for a POC; a real implementation would add a `cancel` op handled on a second thread with `OracleCommand.Cancel()`.

## Supported in the POC

Connect (host, port, service name, user, password, connect and command timeout), run a statement with named parameters (`:name`), stream the result set in batches, read columns (name, Oracle type name, CLR type), DML `RecordsAffected` (autocommit), list schemas (`ListDatabasesAsync` = non-Oracle-maintained users), read tables, views, columns with `NUMBER(p,s)` / `VARCHAR2(n)` types, nullability and primary key into the schema model (so the explorer shows them).

## Not supported (follow-ups)

TLS/TCPS and wallets, SSH tunnel (untested), several result sets and `DBMS_OUTPUT` messages, cancel without killing, transactions control, indexes, foreign keys, column defaults, routines, triggers, view definitions, Oracle dialect for the SQL layer (tokenizer, completion, formatter, DDL generators fall back to the generic ANSI behaviour), terminal (`sqlplus`) launch, CI job, bridge as a pool shared by several connections, Windows-only path of `ResolvePath` untested on Linux and macOS.

## Verification

- 29 unit tests with an in-process fake bridge (codec round trip through JSON, batching, parameters, errors, close, cancel kills, schema mapping).
- 4 tests on a real `gvenzl/oracle-free:23-slim-faststart` container through the real bridge (`rtk pwsh eng/run-docker-tests.ps1 -Engine oracle`, Podman); `oracle` is not part of `all` because of the image size.
- AOT: `Squill.Data.Oracle` builds with `-warnaserror` and the AOT analyzers; the WinUI head still publishes Native AOT with the provider linked in.
