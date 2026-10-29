# P2-05 - Platform: SSH tunnel abstraction and SSH.NET implementation

Status: todo
Depends on: P2-04

## Goal
Allow providers to reach a database through an SSH jump host (risk R7).

## Design decisions (final)
- Namespace `Squill.Platform.Ssh`. `interface ISshTunnel : IAsyncDisposable { int LocalPort { get; } }`; `interface ISshTunnelFactory { ValueTask<ISshTunnel> OpenAsync(SshTunnelSettings settings, string? secret, string remoteHost, int remotePort, CancellationToken ct); }`.
- `SshNetTunnelFactory` (package `SSH.NET` 2026.0.0): `SshClient` with `PasswordAuthenticationMethod` or `PrivateKeyFile(path, secret)` according to `AuthMethod`; `ForwardedPortLocal("127.0.0.1", 0, remoteHost, remotePort)`; `LocalPort` read after `Start()`. Dispose stops the port and disconnects. Host key verification: accept the first key and keep it in memory only (known-hosts persistence is out of scope, noted with a `shortcut:` comment).
- Argument validation (empty host, port outside 1-65535, missing key path for `PrivateKey`) throws `ArgumentException` before any network call; covered by unit tests.
- `tools/Squill.AotProbe.SshNet` (args: host port user password remoteHost remotePort) opens a tunnel and prints `OK <localPort>`; add the row to `docs/poc/aot-drivers.md`. If SSH.NET is not AOT-clean, record the IL warnings and verdict instead of working around them.
- Docker-gated integration test with `linuxserver/openssh-server` is optional; skip it if it takes more than the card's budget.

## Out of scope
- Jump host chains, agent forwarding, known-hosts file.

## Tasks
- [ ] Create contracts and factory; probe tool; update solution and `Directory.Packages.props`.
- [ ] Unit tests for validation; publish the probe and record the verdict.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).
- `rtk dotnet publish tools/Squill.AotProbe.SshNet -c Release -r win-x64` succeeds with zero IL2xxx/IL3xxx warnings.

## Suggested commit message
Add SSH tunnel abstraction and SSH.NET factory
