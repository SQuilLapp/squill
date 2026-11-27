# P21-17 - Connection editor: TLS certificate file fields (Presentation, WinUI, Avalonia)

Status: done
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
- [x] View-model tests (enabled rules, validation messages, secret saved).
- [x] Both heads; drive the editor on each published exe (recipe 3), dark and light.
- [x] README line under "Supported databases", `CHANGELOG.md` line.

## Acceptance criteria
- Recipe 2 and 3 acceptance for both heads.

## Risks
- WinUI file picker cannot be driven by the window driver (P20-10): type the path instead and record the picker as `not driven`.

## Open points (defaults)
- None.

## Suggested commit message
Show the TLS certificate file fields in both connection editors

## Done notes
- Premise checked: the TLS combo, the `TrustServerCertificate` toggle, `ConnectionEditorViewModel` and `IDialogService.PickOpenFileAsync` exist as stated. Both TLS groups already sat inside an `Advanced` expander, so `Advanced TLS` is a nested expander (collapsed by default) under the TLS combo.
- Deviation, enabled rules: only PostgreSQL, MySQL and SQL Server read the P21-16 fields, so the whole `Advanced TLS` expander is hidden for SQLite, Oracle and Informix; CA file for the three; client certificate and key disabled (with the reason `connections.tlsClientUnsupported` as tooltip) for SQL Server; server name for PostgreSQL and SQL Server (MySqlConnector has none); passphrase field only for PostgreSQL (MySqlConnector cannot read encrypted keys). Values of a field that does not apply are dropped by `BuildProfile`, so a hidden leftover never blocks the save.
- Passphrase: new default interface method `IProfileStore.SaveTlsKeyPassphraseAsync` (`JsonProfileStore` stores it under `SecretKeys.TlsKeyPassphrase`; null keeps, empty deletes), called by `SaveAsync` of the editor after `IProfileStore.SaveAsync` (unchanged); loaded by `LoadSecretsAsync`; the connection test passes it through the runtime-only `ConnectionProfile.TlsKeyPassphrase`, the saved profile never carries it.
- Validation: the missing-file and key-without-certificate messages of P21-16 are shown by `Validate()`.
- Browse buttons call the view-model commands (`IDialogService.PickOpenFileAsync`, extensions `pem`/`crt`/`cer`, `pem`/`key`); the heads call them from the button.
- Tests: 8 new view-model tests (rules per engine, validation, Browse, save of files and passphrase, unchanged/cleared passphrase, test passes the passphrase). Full `dotnet test` (both solutions): 0 failures; the Guid fix left by the previous session did not break anything.
- Driven on freshly published exes (the `publish` folders under `src/` were stale, so each head was published to a temp folder): WinUI, dark, French: editor opened, `Advanced` and `TLS avancé` expanded, the five fields and the three `Parcourir...` buttons shown, CA path set through the UI Automation value pattern and shown. The `Parcourir...` button opened the native picker (`PickerHost` covered the window) but the picker was not driven (it does not accept injected input: `not driven`); it was closed by stopping the app. Avalonia, dark, English then light, English: nested expander, fields and Browse buttons present; SQL Server selected: client certificate and key fields disabled, server name enabled, passphrase absent (UI Automation); Avalonia picker not opened. Not run: a TLS PostgreSQL container (the card does not ask for one; the connection side was proven in P21-16). Native AOT checks (`eng/check-aot-warnings.ps1 -Head WinUI` and `-Head Avalonia`): baseline warnings only.
