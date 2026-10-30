# P4-04 - Presentation: connection sessions

Status: done
Depends on: P4-03, P2-05

## Goal
Open a profile into a live session (tunnel + connection + schema).

## Design decisions (final)
- Namespace `Squill.Presentation.Sessions`. `sealed class ConnectionSession : IAsyncDisposable { ConnectionProfile Profile; IDatabaseConnection Connection; SchemaSnapshot Schema; ValueTask RefreshSchemaAsync(CancellationToken); }`.
- `SessionManager(ProviderRegistry, IProfileStore, ISshTunnelFactory)`: `ValueTask<ConnectionSession> OpenAsync(ConnectionProfile p, CancellationToken)` reads the password and SSH secret from `IProfileStore`; when `p.Ssh` is set, opens the tunnel to `p.Host:p.Port` and passes a copy of the profile with `Host = "127.0.0.1"` and the tunnel's `LocalPort`; opens the connection; reads the schema. Failure after the tunnel opened disposes it. `ObservableCollection<ConnectionSession> Sessions`; `CloseAsync(ConnectionSession)` disposes connection then tunnel.
- Cancellation and errors propagate as exceptions (view-models show them).

## Out of scope
- Reconnection, keep-alive.

## Tasks
- [x] Implement both classes.
- [x] Tests with fakes (provider from `Squill.Data.Tests` copied as a test double, fake tunnel factory, in-memory store): host rewriting with SSH, disposal order, failure disposes the tunnel.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add connection session manager
