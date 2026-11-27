# P21-13 - Other engines: DB2, ClickHouse, Snowflake and BigQuery feasibility report

Status: done
Depends on: P21-10

## Goal
The owner may want more engines later. One report says, per engine, which .NET driver exists, whether it can run under Native AOT (measured where cheap), how authentication works, and what the SQuilL model would lack. No product code.

Exists today: five dialects (`DatabaseDialect`), the Oracle bridge pattern, the Informix report of P21-10.

## Design decisions (final)
- Report `docs/poc/other-engines.md`, one section per engine with the same table: driver package and licence, AOT probe result (or `not verified`), container or free tier for tests, auth (password, key pair, OAuth, service account), SQL dialect distance, what does not fit (no transactions, no primary keys, no `information_schema`, billing per query).
- DB2: same `Net.IBM.Data.Db2` as Informix option A: reuse the P21-10 probe result; container `icr.io/db2_community/db2`.
- ClickHouse: `ClickHouse.Client` (ADO.NET over HTTP) or `Octonica.ClickHouseClient` (native protocol); probe one; note: no transactions, no row updates in the grid sense (mutations are asynchronous) - grid editing must stay off.
- Snowflake: `Snowflake.Data`; note: account/warehouse fields, key-pair and SSO auth, costs per running warehouse (a "run" costs money: the production-style confirmation may be wanted); no container (trial account only: `not verified` unless the owner gives one).
- BigQuery: no ADO.NET provider from Google (`Google.Cloud.BigQuery.V2` client library, gRPC/REST); would need a custom `IDatabaseConnection`; dry-run gives bytes billed (could feed the impact estimate); service-account JSON as secret.
- Verdict per engine and a proposed card list with sizes.

## Out of scope
- NoSQL (P21-14), any provider code.

## Tasks
- [x] Probe ClickHouse with a container (cheap); others on paper unless a probe is quick.
- [x] Write the report.

## Acceptance criteria
- Report committed; each claim measured or marked `not verified`.

## Risks
- Cloud engines cannot be tested without accounts; say so rather than guess.

## Open points (defaults)
- Owner: which of the four matters first (default: DB2, since it shares the Informix driver).

## Suggested commit message
Report the feasibility of DB2, ClickHouse, Snowflake and BigQuery
