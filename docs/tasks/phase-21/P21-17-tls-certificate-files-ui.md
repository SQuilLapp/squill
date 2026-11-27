# P21-17 - Connection editor: TLS certificate file fields (Presentation, WinUI, Avalonia)

Status: todo
Depends on: P21-16

## Goal
Show the P21-16 fields in the connection editor of both heads.

Exists today: the TLS mode combo and `TrustServerCertificate` toggle in `ConnectionEditorViewModel` and both `ConnectionEditorWindow`s; `IDialogService.PickOpenFileAsync`.

## Design decisions (final)
- `ConnectionEditorViewModel`: four text properties with `Browse...` commands (`PickOpenFileAsync`, extensions `pem`, `crt`, `key`), the passphrase as a password box stored in `ISecretStore`, enabled rules per dialect (client cert disabled for SQL Server and SQLite, with the reason as a tooltip).
- Both heads: the fields go under the existing TLS options group, collapsed under an `Advanced TLS` expander so the default form does not grow.
- Small UI: one card for both heads (WinUI first in the work order).

## Out of scope
- Certificate store pickers.

## Tasks
- [ ] View-model tests (enabled rules, validation messages, secret saved).
- [ ] Both heads; drive the editor on each published exe (recipe 3), dark and light.
- [ ] README line under "Supported databases", `CHANGELOG.md` line.

## Acceptance criteria
- Recipe 2 and 3 acceptance for both heads.

## Risks
- WinUI file picker cannot be driven by the window driver (P20-10): type the path instead and record the picker as `not driven`.

## Open points (defaults)
- None.

## Suggested commit message
Show the TLS certificate file fields in both connection editors
