# P11-09 - Avalonia head: connection options in the connection manager

Status: done
Depends on: P11-08

## Goal
Same fields as P11-08 in the Avalonia connection manager window, bound to the same view-model members.

## Design decisions (final)
- No new view-model code (P11-08 added it); an "Advanced" expander in the Avalonia connection manager with the same labels (catalog keys from P11-08), field styles of `visual-spec.md`.
- Headless test: load the view for each dialect, assert the controls shown or hidden follow `SupportsTls`, `SupportsTrustCertificate`, `SupportsTimeouts`.

## Out of scope
- Anything else in the Avalonia head.

## Tasks
- [x] AXAML (Advanced expander on the General tab), headless test per dialect.
- [ ] (not driven: the automated session cannot open the editor window without clicking blind on the desktop; do it by hand) Capture SQL Server and SQLite in `en` and `fr` with `tools/visual`.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` 0 warnings; `rtk bypass dotnet test --solution Squill.slnx -c Release` green.
- Avalonia AOT publish with zero IL2xxx/IL3xxx warnings.

## Suggested commit message
Add connection options to the Avalonia connection manager
