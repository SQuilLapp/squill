# Other engines: DB2, ClickHouse, Snowflake and BigQuery

Card [P21-13](../tasks/phase-21/P21-13-other-engines-feasibility.md), measured on 2026-11-27 on Windows 11 x64, .NET SDK 11.0.100, Podman 5.6.2. Throw-away probes (in no solution, `TreatWarningsAsErrors=false` and `ManagePackageVersionsCentrally=false` on purpose): `tools/Squill.AotProbe.ClickHouse`, `ClickHouseNative`, `Snowflake`, `BigQuery`. "Measured" means it was run here; everything else is `not verified` (desk knowledge, no account or server).

## Summary

| Engine | Driver | Native AOT in process | Server for tests | Verdict |
|---|---|---|---|---|
| DB2 LUW | `Net.IBM.Data.Db2` / own ODBC layer on `clidriver` | package: no (P21-10); own ODBC layer: yes against Informix, DB2 itself not verified | `icr.io/db2_community/db2` (not pulled, not verified) | **go through the ODBC layer (P21-103, P21-104)** (after the owner decision on `clidriver`) |
| ClickHouse | `ClickHouse.Client` 7.14.0 (HTTP) | **yes, measured** | `clickhouse/clickhouse-server`, works on Podman | **go**, read-mostly (no grid editing) |
| Snowflake | `Snowflake.Data` 6.5.0 | **no, measured**: the native link step fails | none (trial account only) | **no go in process**; bridge or REST, not verified |
| BigQuery | `Google.Cloud.BigQuery.V2` 3.12.0 (client library, no ADO.NET) | publishes (5 x IL3002 plus trim warnings) and starts; calls not verified | none (cloud only; an emulator exists, not tried) | **go through a custom connection**, low priority, not verified end to end |

## DB2 (Linux/Unix/Windows)

| Item | Value |
|---|---|
| Driver, licence | `Net.IBM.Data.Db2` 10.0.0.400 (IBM International Program License Agreement, redistributable `clidriver` about 125 MB on disk, 46 MB download, terms in [informix-driver.md](informix-driver.md)); or the same `clidriver` through ODBC |
| AOT probe | Measured in P21-10 with the same package and `clidriver`: the package cannot run in a published AOT exe (`Open()` throws `NotSupportedException`, `Assembly.CodeBase`); the own `LibraryImport` layer on `db2cli64.dll` published with 0 warnings and ran a real query against Informix over DRDA. **DB2 itself was not probed**: no DB2 container was started here |
| Test server | `icr.io/db2_community/db2` (desk research: needs `--privileged` on Docker, `LICENSE=accept`, `DB2INST1_PASSWORD`; `not verified` on Podman/WSL) |
| Auth | user/password (the `SQL30082N` diagnostic was readable in P21-10); Kerberos/GSSAPI and TLS (`SECURITY=SSL`) exist in `clidriver`, `not verified` |
| SQL distance | Own tokenizer needed: `FETCH FIRST n ROWS ONLY`, `SYSCAT.*` catalog (not `information_schema`), upper-case identifiers by default, `VALUES` statements, `WITH UR` (desk research). Catalog reading is the main work |
| Does not fit | Db2 for z/OS and IBM i may need a Db2 Connect entitlement (`not verified`); `clidriver` size and licence |

Verdict: `go` once P21-103 and P21-104 exist, because Db2 LUW reuses the ODBC layer (connect, execute, fetch, errors) and only needs its own provider (schema reading from `SYSCAT`) and dialect. Not before the owner's `clidriver` decision.

## ClickHouse

Container: `docker.io/clickhouse/clickhouse-server:latest` (server 26.11.3.10), `-e CLICKHOUSE_USER=... -e CLICKHOUSE_PASSWORD=... -p 127.0.0.1:8123:8123 -p 127.0.0.1:9000:9000`. Started without options on Podman/WSL; removed after the run.

| Item | Value |
|---|---|
| Driver, licence | `ClickHouse.Client` 7.14.0 (ADO.NET over HTTP 8123); alternative `Octonica.ClickHouseClient` 4.1.8 (native protocol 9000). Licences: `not verified` (believed MIT) |
| AOT probe, `ClickHouse.Client` | **publish succeeds with only the assembly-level IL2104 and IL3053 of the package, none from SQuilL code**; exe 8.8 MB; the published exe connected, `ServerVersion` `26.11.3.10`, and returned columns and rows for `UInt64`, `String`, `Decimal64(3)` (as `ClickHouseDecimal`), `DateTime64(3, UTC)`, `Nullable(UInt8)` and a `system.tables` query. **Go under AOT** |
| AOT probe, `Octonica` 4.1.8 | publish succeeds with IL2104 and IL3053; against server 26.11 the default compressed protocol failed: `An unexpected compression algorithm identifier was received. Expected value: 0x82. Actual value: 0x90.`; with `Compress=false` it worked (`Decimal(18, 3)` as `System.Decimal`). Not chosen: it needs a workaround on current servers |
| Auth | user/password (measured); TLS via HTTPS 8443 and tokens `not verified` |
| SQL distance | `LIMIT`, `FORMAT`, `system.*` catalog; `information_schema` exists as a compatibility view (measured: 207 tables on an empty server) but `system.tables` and `system.columns` are complete. Measured: a `MergeTree` table reports `primary_key` and `sorting_key` (`id`), and that key is not unique |
| Does not fit | **No transactions** (measured: `BEGIN TRANSACTION` gives `Code: 48 ... Transactions are not supported`); **no row updates in the grid sense**: `ALTER TABLE ... UPDATE` is an asynchronous mutation (measured: it returns at once and shows up in `system.mutations`), so grid editing, key read-back and the commit/rollback model must stay off; no foreign keys; `ClickHouseDecimal` needs a conversion |

Verdict: `go`, read-only first.

## Snowflake

| Item | Value |
|---|---|
| Driver, licence | `Snowflake.Data` 6.5.0 (ADO.NET); licence `not verified` (believed Apache 2.0) |
| AOT probe | **publish fails in the native link step**: `sf_mini_core.dll : fatal error LNK1107: invalid or corrupt file` (the package carries `runtimes\win-x64\native\sf_mini_core.dll`, 109.5 KB, which its build props hand to the linker). Before that the package raises IL2104, IL3053, IL3000 (`SFEnvironment.ExtractApplicationPath`, `Assembly.Location`), IL3002 and warnings from `Newtonsoft.Json`, `Google.Apis` (file transfer), `System.Management` and `Tomlyn.Signed` (IL2101). No exe, so nothing ran. Whether an MSBuild property removes the native-library link was **not tried** |
| Test server | none (cloud only, trial account); everything that needs a server is `not verified` |
| Auth | password, key pair, SSO, OAuth (desk research); account identifier, user, role, warehouse, database and schema form the connection |
| SQL distance | ANSI-like with `LIMIT`, `QUALIFY`, `VARIANT`, `OBJECT`, `ARRAY`, `information_schema` per database (desk research) |
| Does not fit | A running warehouse bills credits, so each "Run" costs money (a production-style confirmation may be wanted); warehouses suspend and resume; multi-statement scripts need a driver setting (all desk research) |

Verdict: `no go` in process under Native AOT as measured. Not measured alternatives: a bridge process on JIT ([oracle-bridge.md](oracle-bridge.md)) or an own client for the Snowflake SQL REST API (HTTPS plus source-generated JSON, key-pair JWT or OAuth token, no native part). No cards before the owner has a trial account.

## BigQuery

| Item | Value |
|---|---|
| Driver, licence | `Google.Cloud.BigQuery.V2` 3.12.0, a client library with **no ADO.NET provider from Google**; SQuilL would need a custom `IDatabaseConnection`. Licence `not verified` (believed Apache 2.0) |
| AOT probe | publish succeeds: 5 x IL3002 from `Google.Apis.Requests.VersionHeaderBuilder.GetEntryAssemblyVersionOrNull` (`Assembly.CodeBase`, `EscapedCodeBase`, `GetFile`, `GetFiles`; the same API class that breaks the IBM package, here on the user-agent path), plus IL2104/IL3053 for `Google.Apis`, `Google.Apis.Core`, `Newtonsoft.Json`, `System.Management`, `Microsoft.CSharp`, `System.Linq.Expressions`. The exe ran: building the client reached the credential lookup and failed properly with `InvalidOperationException: Your default credentials were not found`. **No query was executed** (no project), so whether the `CodeBase` calls throw at request time is `not verified` |
| Test server | none (the community emulator was not tried) |
| Auth | service-account JSON key (a secret for `ISecretStore`), Application Default Credentials, OAuth user flow (desk research) |
| SQL distance | GoogleSQL: backtick identifiers `project.dataset.table`, `STRUCT`, `ARRAY`, `INFORMATION_SCHEMA` per dataset (desk research) |
| Does not fit | Billed per bytes scanned: a dry run returns the bytes a query would process (desk research), which could feed the production-style confirmation; no useful key constraints; jobs and result pages instead of a cursor; `Newtonsoft.Json` and `Google.Apis` are reflection-heavy (the warnings above) |

Verdict: `go through a custom connection`, low priority, not verified end to end. Preferred route if wanted: an own REST client (`jobs.query`, `jobs.getQueryResults`, dry run) with source-generated JSON and a service-account JWT signed with `System.Security.Cryptography`, avoiding the Google libraries (desk research).

## Cards to write next (sizes in Oracle-card units)

| Order | Card | Size | Condition |
|---|---|---|---|
| 1 | `Data.ClickHouse` provider (read-only capability, schema from `system.*`), dialect, Docker engine value `clickhouse` and ADR 0008 row | L + M + S | none, measured; check first how the session model expresses "no transactions, no editing" |
| 2 | Db2 LUW provider and dialect on the ODBC layer, preceded by a probe against `icr.io/db2_community/db2` | S + L + M | after P21-104 (clidriver accepted by the owner on 2026-11-27) |
| 3 | BigQuery REST connection with dry-run bytes in the confirmation | L | owner has a project |
| 4 | Snowflake (bridge or REST) | L | owner has a trial account |

## Open decisions for the owner

- Which engine first (the card defaults to DB2; ClickHouse is the only one measured end to end and needs no licence).
- ClickHouse as a read-only engine in the support matrix of [ADR 0008](../adr/0008-engine-parity.md).
- Snowflake and BigQuery: a trial account or project to test with, and bridge versus own REST client.
- The `clidriver` shipping decision of P21-10 (blocks DB2).

## Cleanup

The container `squill-clickhouse` was removed. The probes are not in any solution; their `bin` and `obj` are git-ignored.
