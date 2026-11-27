# P21-19 - Connection editor: proxy fields (Presentation, WinUI, Avalonia)

Status: done
Depends on: P21-18

## Goal
Set the proxy of P21-18 in the connection editor of both heads.

Exists today: the SSH section of the connection editor (both heads) with its enable toggle, host, port, user and secret.

## Design decisions (final)
- `ConnectionEditorViewModel`: a `Proxy` section next to SSH (kind combo `None`, `SOCKS5`, `HTTP`; host, port, user, password stored in `ISecretStore`), validation (port 1 ... 65535, host required), included in `Test connection`.
- Both heads: a new collapsible group under SSH, same styles. One card for both heads.

## Out of scope
- System proxy, PAC.

## Tasks
- [x] View-model tests; both heads; drive each published exe (recipe 3) with the proxy container of P21-18.
- [x] README line, `CHANGELOG.md` line.

## Acceptance criteria
- Recipe 2 and 3 acceptance for both heads.

## Risks
- None beyond P21-18.

## Open points (defaults)
- None.

## Suggested commit message
Show the proxy fields in both connection editors

## Done notes
- Premise checked: the SSH section is a tab (`SSH`) of both editors with the enable toggle, host, port, user, auth and secret, as stated; `ConnectionProfile.Proxy`, `ProxySettings` and `IProfileStore.GetProxySecretAsync` / `SaveProxySecretAsync` exist from P21-18. Deviation: the card says "a new collapsible group under SSH"; it is a `Proxy` expander (collapsed) inside the SSH tab, visible even when the SSH toggle is off, hidden for SQLite. The first WinUI layout (host and port side by side in `*,16,220`) was clipped in the narrow tab column; host and port are now `*,12,100` and user and password are stacked, in both heads.
- `ConnectionEditorViewModel`: `ProxyKindIndex` (0 none, 1 SOCKS5, 2 HTTP; default port 1080 / 3128 follows the kind unless the user changed it), `ProxyHost`, `ProxyPort`, `ProxyUserName`, `ProxyPassword`, `SupportsProxy`, `UseProxy`; validation `connections.errProxyHost` / `errProxyPort` only while a proxy is selected; `BuildProfile` writes `ProxySettings` (host trimmed). The password goes only through `SaveProxySecretAsync` (null keeps it while unchanged, empty deletes it when the proxy is removed or has no user); it is loaded by `LoadSecretsAsync`.
- Test connection: optional constructor parameter `IProxyTunnelFactory` (default `ProxyTunnelFactory`); the test opens the forwarder and connects the provider to `127.0.0.1:<port>` with `TlsServerName` falling back to the real host, as `SessionManager` does. With SSH enabled the test does not use the proxy (the test already ignored SSH, and then the proxy carries the SSH connection). Errors come from `ProxyHandshake` and never contain the password.
- Tests: 5 new view-model tests (default ports and SQLite, validation, save keeps the password out of the profile, unchanged / removed secret, test through the proxy with the password never in the failure text); the catalog allow list got the three identical words (`Proxy`, `SOCKS5`, `HTTP CONNECT`). Full `dotnet test` of `Squill.Windows.slnx`: 0 failures.
- Driven on freshly published exes in a temp folder with a PostgreSQL container and a `serjs/go-socks5-proxy` container (user and password) on a Podman network, the proxy port published on loopback only. WinUI, dark, French: `Proxy` expander and kind combo set through UI Automation; a wrong proxy password gives the error dialog `The SOCKS5 proxy refused the user name or password.`, the right one gives `Connexion réussie 17.5`; Save writes `profiles.json` with `proxy` (kind, host, port, user) and no password, the two secrets are DPAPI `.bin` files only. Avalonia, light, English: opened the saved profile, the proxy fields and the password (10 bullets) were loaded, `Connection successful 17.5` through the proxy. HTTP CONNECT through the UI: not driven (covered by the P21-18 unit and Docker tests). Native AOT checks of both heads: baseline warnings only. Containers, network, exes and temp folder removed.
