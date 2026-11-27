# NoSQL: MongoDB and Redis

Card [P21-14](../tasks/phase-21/P21-14-nosql-feasibility.md), measured on 2026-11-27 on Windows 11 x64, .NET SDK 11.0.100, Podman 5.6.2. Throw-away probes (in no solution, `TreatWarningsAsErrors=false` and `ManagePackageVersionsCentrally=false` on purpose): `tools/Squill.AotProbe.MongoDb`, `tools/Squill.AotProbe.Redis`. "Measured" means run here against `docker.io/library/mongo:8.0` (server 8.0.12) and `docker.io/library/redis:7` (server 7.4.12); everything else is `not verified` (desk knowledge). The JSON tree viewer of the card (P21-51) is itself a `todo` card, not code.

## Summary

| Store | Driver | Licence | Native AOT in process | Fit with SQuilL | Verdict |
|---|---|---|---|---|---|
| MongoDB | `MongoDB.Driver` 3.14.1 | Apache-2.0 (nuspec) | **no, measured**: publishes, then fails at the first server handshake | explorer, connection, JSON viewer fit; grid partly; designer, ER, SQL editor, transactions do not | **no go in process**; bridge process or own wire client, not verified |
| Redis | `StackExchange.Redis` 3.4.35 | MIT (nuspec) | **yes, measured**: 0 IL warnings, ran every call tried | key browser and value viewer are new UI; nothing of the SQL model fits | **go technically**, but only as a separate tab kind and only if the owner wants NoSQL |

## MongoDB

| Item | Result |
|---|---|
| Publish | succeeds; only the assembly-level `IL2104` and `IL3053` for `MongoDB.Driver` and `MongoDB.Bson`; exe 12.9 MB. No warning comes from probe code, which used `BsonDocument` only (no POCO class maps) |
| Run, published exe | **fails**: `TimeoutException` selecting a server, whose inner exception is `MissingMethodException: No suitable constructor found for serializer type: 'EnumerableInterfaceImplementerSerializer'2[[List'1[[Object...` raised in `BsonSerializationProviderBase.CreateSerializer` while decoding the first `hello` reply (`BsonDefaults.DynamicArraySerializer`). The serializer registry builds serializers by reflection, which trimming removed. The driver never gets past connecting, so no query ran |
| Run, JIT control (`dotnet run`, same code, same container) | works: `find` with a JSON filter, `aggregate` (`$group`), `ListCollectionNames`, `Indexes.List`; so the container and the probe are fine and the failure is AOT only |
| Whether `BsonSerializer.RegisterSerializer`, a trimmer root file or another driver version would cure it | **not tried**. The failure is in the driver's own start-up path, so a fix would have to live in the package; no workaround proven |
| Auth | user/password SCRAM (`mongodb://user:pass@`), TLS, `mongodb+srv` and Atlas are desk research, `not verified` |

What a MongoDB tab would need (desk design, nothing measured):

- **Data model**: a collection has no fixed columns. The grid can show a "flatten the top-level fields of the first N documents" view (union of keys as columns, missing = empty, nested values as a JSON cell opened in the JSON tree of P21-51). That fits `IResultReader` (`Columns` is a list the reader can compute after sampling), but the column set is a guess, not a schema, and types vary per row.
- **Query language**: not SQL. `find` filter JSON, projection, sort and aggregation pipelines need their own editor mode, tokenizer and completion; the SQL tokenizer, `SqlRunner`, statement splitting and the dialect layer do not apply.
- **Designer and ER diagram**: **do not fit** (no schema, no foreign keys; `$jsonSchema` validators are optional and rare).
- **Explorer**: databases, collections and indexes (`ListCollectionNames`, `Indexes.List`, both measured on JIT) fit the tree; there are no tables, columns or views in the SQL sense, so `SchemaSnapshot`/`TableInfo` would be stretched, not reused.
- **Transactions and editing**: multi-document transactions exist only on replica sets and sharded clusters (desk research); the container here is a standalone server. The grid's commit/rollback model, key-based read-back and the phase 15 production confirmation would each need a document variant (`_id` is the key). Editing a nested document in a grid cell does not fit; a JSON document editor does.

Verdict: `no go in process under Native AOT` as measured with 3.14.1. Not measured alternatives: a bridge process on JIT ([oracle-bridge.md](oracle-bridge.md)), or an own client for the Mongo wire protocol (OP_MSG, BSON read/write by hand, SCRAM) with no reflection; both are large for a feature outside the SQL scope.

## Redis

| Item | Result |
|---|---|
| Publish | succeeds with **0 IL2xxx/IL3xxx warnings** (counted on a forced rebuild); exe 7.7 MB |
| Run, published exe | works: `server.Version` 7.4.12, `SCAN` through `server.KeysAsync(pattern)`, `TYPE` for string, hash, list, set, sorted set and stream, `TTL`, `HGETALL`, `ZRANGE ... WITHSCORES`, `XADD`/`XRANGE`, raw `ExecuteAsync("SET", ...)` returned `OK`. **Go under AOT** |
| Admin commands | `ExecuteAsync("INFO", ...)` is refused client-side (`RedisCommandException: This operation is not available unless admin mode is enabled: INFO`) until the connection string has `allowAdmin=true`; with it, it worked. A command console needs that option (and then `FLUSHALL`/`CONFIG` are reachable: a confirmation like the production-style one would be wanted) |
| Auth | password and ACL user are connection-string options, TLS `ssl=true`, Sentinel and Cluster are driver features; all `not verified` here |

What a Redis tab would need (desk design):

- **Key browser**: `SCAN` with a pattern (never `KEYS` on a large server), `TYPE` and `TTL` per key, a value viewer per type: string and JSON string (JSON tree), hash (the only grid-shaped value: field/value columns), list, set, sorted set (member/score) and stream (entries). Each is a different small viewer, not one grid.
- **Commands**: a console in the style of the terminal tabs fits better than the query tab; there is no SQL, no statement text model, no result set with a stable schema.
- **Does not fit**: designer, ER diagram, schema snapshot, SQL completion, SQL transactions (`MULTI`/`EXEC` is queued atomic execution, no rollback of effects), and "tables" in the explorer (databases are numbered, keys are a flat namespace, folders are only a naming convention with `:`).

## What would be shared and what is new

| Shared | New |
|---|---|
| Connection profiles and the profile store (a new kind), `ISecretStore` for passwords, SSH tunnel, the explorer tree shell, the JSON tree viewer of P21-51, the terminal console tab for commands | A non-SQL "document tab" (MongoDB) and a "key tab" (Redis) with their own view-models, which cannot be the SQL query tab: they have no SQL text, no `SqlRunner`, no `StatementScope`, no transaction session and no designer |

The recipe rule that every write goes through `SqlRunner`/`StatementScope` and the session transaction cannot be kept for NoSQL; a separate write path would need its own rule and its own production confirmation, which is a design decision for the owner, not something to slip into a card.

## Does the grid, designer and transaction model fit?

No, except in small parts. The grid fits a flattened, sampled view of documents and the fields of one hash; the designer, the ER diagram, SQL completion and the commit/rollback session fit neither store. The honest shape is separate tab kinds, as the card guessed, and this is a second product surface, not an engine row in [ADR 0008](../adr/0008-engine-parity.md).

## Cards to write next (only if the owner puts NoSQL in scope)

| Order | Card | Size | Condition |
|---|---|---|---|
| 1 | Redis: `StackExchange.Redis` connection profile kind, key browser (`SCAN`, `TYPE`, `TTL`) and read-only value viewers; Docker engine value `redis` | L + M | owner says NoSQL is in scope; AOT measured here |
| 2 | Redis command console on the terminal tab (`allowAdmin`, confirmation for destructive commands) | M | after 1 |
| 3 | MongoDB: decision first (bridge process or own wire client), then explorer and a read-only document tab | proof of concept + L | owner decision; the official driver is blocked under AOT |

## Open decisions for the owner

- Is NoSQL in scope at all (default of the card: no, until asked again).
- If yes: Redis first (measured, small, MIT) and MongoDB only through a bridge or an own client.
- Whether NoSQL writes may bypass `SqlRunner`/session transactions, and with which confirmation.

## Cleanup

The containers `squill-mongo` and `squill-redis` were removed. The probes are not in any solution; their `bin` and `obj` are git-ignored.
