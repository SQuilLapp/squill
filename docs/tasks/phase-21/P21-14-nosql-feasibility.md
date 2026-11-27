# P21-14 - NoSQL: MongoDB and Redis feasibility report

Status: done
Depends on: none

## Goal
Say honestly whether MongoDB and Redis fit SQuilL, and what it would take. No product code.

Exists today: everything in SQuilL assumes tables, columns, SQL text and a schema snapshot (`SchemaSnapshot`, `IResultReader` with fixed columns, the table designer, the ER diagram, completion over SQL).

## Design decisions (final)
- Report `docs/poc/nosql.md`:
  - **MongoDB**: driver `MongoDB.Driver` (AOT status to probe: it uses reflection-based BSON class maps; `BsonDocument` without POCOs may avoid most of it - measure with a probe console); documents have no fixed columns, so the grid needs a "flatten top-level fields of the first N documents" view, nested values in the JSON tree viewer (P21-51); the query language is not SQL (a separate editor mode: `find` filter JSON, aggregation pipeline); designer and ER diagram **do not fit** (no schema); indexes and collections fit the explorer.
  - **Redis**: `StackExchange.Redis` (AOT status to probe); a key browser with a value viewer per type (string, hash, list, set, sorted set, stream); no SQL, no grid model beyond hash fields; commands typed in the terminal-like console fit better than the query tab.
  - What would be shared: connection profiles, secrets, SSH tunnel, the explorer tree, the JSON viewer. What would be new: a non-SQL "document tab" and a "key tab".
- Verdict: probably `separate tab kinds, not the SQL query tab`; propose cards only if the owner wants it.

## Out of scope
- Any code beyond a throw-away probe.

## Tasks
- [x] Probe both drivers with Native AOT against containers (`mongo:8`, `redis:7`), record warnings.
- [x] Write the report.

## Acceptance criteria
- Report committed with measured probe results.

## Risks
- None for the app (report only).

## Open points (defaults)
- Owner: is NoSQL in scope at all for SQuilL (default: no until asked again).

## Suggested commit message
Report whether MongoDB and Redis fit SQuilL

## Done notes
Report: [docs/poc/nosql.md](../../poc/nosql.md). Measured: MongoDB.Driver 3.14.1 publishes under AOT but fails at the first handshake (MissingMethodException in the BSON serializer registry; JIT control works); StackExchange.Redis 3.4.35 publishes with 0 IL warnings and runs every call tried. Verdict: Redis go technically, MongoDB no go in process; both only as separate tab kinds and only if the owner wants NoSQL.
