# P21-18 - Core and Platform: SOCKS5 and HTTP CONNECT proxy for connections

Status: todo
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
- [ ] Unit tests with an in-process fake SOCKS5 and HTTP proxy (handshake, auth, refusal, remote DNS).
- [ ] Docker-gated test: a PostgreSQL container reached through a `serjs/go-socks5-proxy` (or similar) container.

## Acceptance criteria
- Recipe 2 common acceptance; Docker-gated proxy test green.

## Risks
- A forwarder bug can leak sockets: dispose every pair when either side closes; test with 100 connections.

## Open points (defaults)
- None.

## Suggested commit message
Forward database connections through a SOCKS5 or HTTP CONNECT proxy
