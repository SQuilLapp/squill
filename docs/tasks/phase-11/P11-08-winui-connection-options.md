# P11-08 - WinUI head: connection options in the connection editor

Status: done
Depends on: P11-06, P11-07

## Goal
The user can set TLS mode, "trust server certificate" and the two timeouts in the connection editor of the WinUI head, with correct state per engine.

## Design decisions (final)
- `ConnectionEditorViewModel` (Presentation) exposes `Tls` (the four modes), `TrustServerCertificate`, `ConnectTimeoutSeconds`, `CommandTimeoutSeconds` as text-parsed numbers with validation (P11-06 codes), and `SupportsTls` / `SupportsTrustCertificate` (true only for SqlServer) / `SupportsTimeouts` flags per dialect (SQLite: command timeout only). They go into the saved profile and are read back when editing. Presentation tests per dialect.
- The WinUI connection editor (`ConnectionEditorWindow`) gets an "Advanced" expander below the existing fields; the controls bind to the view-model and follow the visual spec (`docs/design/visual-spec.md`, form rows, same field styles as host and port); labels, hints and error texts through the catalog (fr and en).
- The Avalonia head is P11-09; do not touch it here.

## Out of scope
- Driver mapping (P11-07).

## Tasks
- [x] View-model members and tests; XAML and `Loc` keys in both catalogs.
- [ ] (not driven: the automated session could not open the editor window, `Ctrl+,` opens Settings, and clicking blind on the desktop is unsafe; do it by hand or with a click-capable tool) Capture the editor for each of the four dialects with `SQUILL_LANGUAGE=en` and `fr` (tools of P9-02) and read the images.

## Acceptance criteria
- `rtk dotnet build Squill.Windows.slnx -c Release -warnaserror` 0 warnings; `rtk bypass dotnet test --solution Squill.Windows.slnx -c Release` green.
- WinUI AOT publish with zero IL2xxx/IL3xxx warnings.
- Native AOT publish of `src/Squill.App.WinUI`: only the pre-existing IL2104/IL3053 aggregate warnings of the third-party `Microsoft.Data.SqlClient` and `System.Configuration.ConfigurationManager` assemblies, none from SQuilL code.
- To check by hand: open the editor, switch dialect, enter a negative timeout (message shown), save, reopen (values kept).

## Suggested commit message
Add connection options to the WinUI connection editor
