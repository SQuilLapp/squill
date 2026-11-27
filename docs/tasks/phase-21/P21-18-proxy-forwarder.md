# P21-18 - Core and Platform: SOCKS5 and HTTP CONNECT proxy for connections

Status: done
Depends on: none

## Goal
Reach a database through a corporate proxy, as DBeaver allows (DataGrip: SOCKS/HTTP proxy).

Exists today: SSH tunnels (`SshTunnelSettings`, `SshNetTunnelFactory`) that forward a local port to the database; no proxy. None of the four drivers speaks SOCKS or HTTP CONNECT itself.

## Design decisions (final)
- Reuse the tunnel shape: a local loopback listener that forwards each accepted socket through the proxy (`ProxyTunnelFactory`, same contract as the SSH tunnel factory so `SessionManager` treats both alike). The provider connects to `127.0.0.1:<port>`.
- Profile field `Proxy` (`ProxySettings`: `Kind` `Socks5` | `HttpConnect`, host, port, user); password in `SecretKeys.ProxySecret(id)`. SSH and proxy together: the proxy carries the SSH connection (SSH.NET `ProxyTypes` supports SOCKS5 and HTTP): no double forwarder.
- SOCKS5 (RFC 1928, user/password RFC 1929) and HTTP CONNECT (Basic auth) written by hand over `Socket` (about 150 lines, no package); remote DNS resolution by the proxy (SOCKS5 domain name address type).
- TLS `VerifyFull` keeps working through `TlsServerName` (P21-16) or the profile host.

## Out of scope
- UI (P21-19), PAC files, NTLM/Kerberos proxy auth, system proxy detection.

## Tasks
- [x] Unit tests with an in-process fake SOCKS5 and HTTP proxy (handshake, auth, refusal, remote DNS).
- [x] Docker-gated test: a PostgreSQL container reached through a `serjs/go-socks5-proxy` (or similar) container.

## Acceptance criteria
- Recipe 2 common acceptance; Docker-gated proxy test green.

## Risks
- A forwarder bug can leak sockets: dispose every pair when either side closes; test with 100 connections.

## Open points (defaults)
- None.

## Suggested commit message
Forward database connections through a SOCKS5 or HTTP CONNECT proxy

## Done notes
- Premise checked: `SshTunnelSettings`, `SshNetTunnelFactory`, `ISshTunnel`, `SessionManager` and the secret keys exist as stated; no proxy code existed. Deviation: the card says "same contract as the SSH tunnel factory"; the proxy forwarder reuses `ISshTunnel` (loopback port plus dispose) and has its own `IProxyTunnelFactory` (the SSH factory needs a host key prompt that a proxy has no use for).
- Added `ProxySettings` / `ProxyKind` and `ConnectionProfile.Proxy` (last, optional member: older `profiles.json` load unchanged), `SecretKeys.ProxySecret`, default interface members `IProfileStore.GetProxySecretAsync` / `SaveProxySecretAsync` (password only in the secret store, deleted with the profile). `Squill.Platform/Proxy`: `ProxyHandshake` (SOCKS5 RFC 1928/1929 with the domain-name address type so the proxy resolves the host, HTTP CONNECT with Basic auth, written over `Socket`, no package) and `ProxyTunnelFactory` / `ProxyTunnel` (loopback listener, one handshake at open to fail early with the proxy message, kept for the first connection; every pair is disposed when either side closes or the tunnel is disposed).
- SSH and proxy together: new default interface overload `ISshTunnelFactory.OpenAsync(..., ProxySettings, proxySecret, ...)`; `SshNetTunnelFactory` passes SOCKS5 / HTTP to SSH.NET's `ConnectionInfo`, so no second forwarder. `SessionManager` picks SSH (through the proxy when set), proxy only, or direct; the host is rewritten to `127.0.0.1` and `TlsServerName` keeps the real host (same as SSH).
- Proxy errors never contain the password (tested). Limit: the handshake socket kept for the first connection can be closed by a proxy idle timeout before the database driver connects (the driver then sees a reset and retries).
- Tests: 17 in-process unit tests (`ProxyTunnelTests`: both protocols open and with credentials, wrong password, refusal, auth required, argument validation, unreachable proxy, 100 connections leave 0 sockets, dispose closes everything), a profile store test (file has no password, secret deleted with the profile), 2 `SessionManager` tests. Docker (`pwsh eng/run-docker-tests.ps1 -Engine proxy`, new value, not part of `all`): 4 tests green on Podman (SOCKS5 with password and a wrong password, HTTP CONNECT through squid, SSH connection carried by the SOCKS5 proxy), PostgreSQL and SSH containers without published ports. The HTTP proxy container runs without auth (Basic auth is covered by the unit tests only). The database check is a PostgreSQL SSLRequest answer, as in the SSH tests.
- Not run: AOT publish (no head code changed; the forwarder uses only `Socket` / `NetworkStream`).
