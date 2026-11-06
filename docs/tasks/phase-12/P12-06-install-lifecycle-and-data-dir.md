# P12-06 - Install lifecycle: first run, update hooks, uninstall, data directory kept

Status: todo
Depends on: P12-01, P12-02

## Goal
Installing, updating and uninstalling never touch the user's data in `%APPDATA%\SQuilL` (profiles, encrypted secrets, history, snippets, settings, diagram layouts), and the app knows when it runs for the first time after an install or an update.

## Design decisions (final)
- Audit first, in the report: grep `src/` for every write path (`AppContext.BaseDirectory`, `Environment.CurrentDirectory`, relative paths, `Path.GetTempPath`). Current known reads from `AppContext.BaseDirectory` are icons and fonts (read-only, fine). Any write under the install root is a bug to fix in this card (move it under `AppPaths`). The Oracle bridge (`eng/OracleBridge.targets`) must be checked: say where its files live and whether they are inside the publish folder (read-only) or written at run time.
- `Squill.Platform`: `InstallLifecycle` (public static class, `Squill.Platform.Lifecycle`) with `string StateFileName = "install-state.json"` and a record `InstallState(string LastRunVersion)` persisted through a source-generated context (`InstallStateJsonContext`) in `AppPaths.DataDirectory` by `InstallStateStore(AppPaths)` (`Load`, `Save`, synchronous, tiny file, document why). `RunKind Classify(string? lastRunVersion, string currentVersion)` returns `FirstRun` (no state), `Updated` (`SemanticVersion` of last < current), `Downgraded` (greater), `Same`. Used by P12-11 (about / "what's new").
- WinUI `Program.Main`: `VelopackApp.Build()` gets `.OnBeforeUninstallFastCallback(...)` (and the other hooks only if P12-01 says they are needed): the uninstall hook removes **nothing** from `%APPDATA%\SQuilL`; it writes no file and shows no UI (hooks have seconds). `OnFirstRun` / `OnAfterUpdate` hooks do nothing visible either: the classification above runs in the normal startup, because hooks run in a short-lived process. Document in a comment why each hook is empty or absent.
- The uninstaller leaves `%APPDATA%\SQuilL` in place by design (documented in `docs/release.md` by P12-20: "delete the folder by hand to remove your data"). No prompt.
- Never log or persist secrets; `install-state.json` holds only the version string.
- Windows Credential / DPAPI secret store keeps working across updates because it is user-scoped, not path-scoped: add a test `SecretStore_AfterReinstall...` is not possible; instead state it in the report from `SecretStoreFactory` reading.

## Out of scope
- Update UI, update service, any dialog about data deletion on uninstall.

## Tasks
- [ ] Audit + fixes (smallest possible), `InstallLifecycle`, `InstallStateStore`, hook wiring in `Program.cs`.
- [ ] Startup call (head-independent): `AppComposition.Create` reads the state, classifies against `AppVersion.Current`, exposes `RunKind` on `AppServices` (add an optional member at the end of the record; heads unchanged), then saves the current version. A throwing state file (corrupt JSON) is treated as `FirstRun` and overwritten.
- [ ] Tests in `tests/Squill.Platform.Tests` with `AppPaths(tempDir)`: classify table, store round trip, corrupt file, state file written in the data dir and nowhere else, `install-state.json` content is exactly the version.

## Acceptance criteria
- `rtk dotnet build Squill.Windows.slnx -c Release -warnaserror` 0 warnings; `rtk bypass dotnet test --solution Squill.Windows.slnx -c Release` green.
- WinUI AOT publish (`rtk dotnet publish src/Squill.App.WinUI -c Release -r win-x64`) with zero IL2xxx/IL3xxx warnings; the published exe started with a throwaway `SQUILL_DATA_DIR` creates `install-state.json` with `0.1.0` (or the current version) and exits cleanly when closed; no process left running.
- The audit table is in the report (path, read or write, verdict).

## Suggested commit message
Keep user data out of the install root and track the last run version
