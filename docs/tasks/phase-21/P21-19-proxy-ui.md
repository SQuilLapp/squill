# P21-19 - Connection editor: proxy fields (Presentation, WinUI, Avalonia)

Status: todo
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
- [ ] View-model tests; both heads; drive each published exe (recipe 3) with the proxy container of P21-18.
- [ ] README line, `CHANGELOG.md` line.

## Acceptance criteria
- Recipe 2 and 3 acceptance for both heads.

## Risks
- None beyond P21-18.

## Open points (defaults)
- None.

## Suggested commit message
Show the proxy fields in both connection editors
