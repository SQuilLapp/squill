# P18-11 - Data: server activity contract and PostgreSQL reader

Status: todo
Depends on: none

## Goal
A read-only snapshot of what a server is doing (sessions, waits, who blocks whom) behind one engine-neutral contract, and the PostgreSQL implementation. Recipe 1, 4.

## Design decisions (final)
- Core model (`src/Squill.Core/Activity/`): `ActivitySnapshot(DateTimeOffset TakenAt, int ActiveConnections, int? MaxConnections, long? TransactionCounter, IReadOnlyList<ServerSession> Sessions, IReadOnlyList<BlockingEdge> Blocking)`; `ServerSession(string Id, string? User, string? Application, string? Client, SessionState State, TimeSpan? Duration, string? WaitEvent, string? Query, DateTimeOffset? TransactionStart, DateTimeOffset? StateChange, int? LocksHeld, bool IsSystem)`; `SessionState` `Active | Idle | IdleInTransaction | Waiting | Other` (the engine's own wording is kept in a `StateText` string for display, as M12 shows `idle in transaction`); `BlockingEdge(string BlockerId, string BlockedId, string? LockMode, string? Relation)`.
- Contract (`src/Squill.Data/IActivityReader.cs`), optional capability found like `IPlanReader` (read how `IPlanReader` is exposed on the connection and copy the pattern): `ValueTask<ActivitySnapshot> ReadAsync(CancellationToken)`, `ValueTask CancelQueryAsync(string sessionId, CancellationToken)`, `ValueTask TerminateSessionAsync(string sessionId, CancellationToken)`. `null` capability = unsupported (SQLite, Oracle).
- PostgreSQL (`Squill.Data.Postgres/PostgresActivityReader.cs`): `pg_stat_activity` (`pid`, `usename`, `application_name`, `client_addr`/`client_port`, `state`, `now() - query_start` or `xact_start`, `wait_event_type`/`wait_event`, `query`, `xact_start`, `state_change`, `backend_type` for `IsSystem` = not `client backend`), `pg_blocking_pids(pid)` for the edges, `pg_locks` counts per pid (`LocksHeld`; the mode and relation of the blocking lock from `pg_locks` + `pg_class`), `max_connections` from `current_setting`, `xact_commit + xact_rollback` of the current database from `pg_stat_database` as `TransactionCounter`. The reader's own backend is excluded (`pid <> pg_backend_pid()`). Limit `query` to 4,000 characters. A permission shortfall (non-superuser sees blank queries of other users) is not an error; fields stay null.
- `CancelQueryAsync` = `select pg_cancel_backend($1)`, `TerminateSessionAsync` = `select pg_terminate_backend($1)`; both parameterised (the id is a number, parse it, reject anything else with `ArgumentException`), return normally when the server says `false` after raising `ActivityActionFailedException` (new, with a message code) so the UI can say "refused".
- No statement the reader sends writes anything except those two calls.

## Out of scope
- MySQL, SQL Server (P18-12), Presentation, heads.

## Tasks
- [ ] Core model, contract, PostgreSQL reader, provider hook, `ActivityActionFailedException`.
- [ ] Tests: model and parse helpers unit tested without a server; Docker-gated `tests/Squill.Data.Postgres.Tests/ActivityReaderTests.cs`: a second connection holds a transaction open (`idle in transaction`), a third updates the same row and waits: snapshot shows both states, the edge blocker -> blocked, the lock info, `MaxConnections`; cancel makes the blocked statement fail with the cancel error; terminate removes the session; the reader's own pid is absent; a non-numeric id is rejected.

## Acceptance criteria
- Common acceptance (recipe 7), no head touched; the Docker-gated test ran on PostgreSQL (say so in the report).

## Suggested commit message
Add the server activity contract and the PostgreSQL reader
