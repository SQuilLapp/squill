# P21-111 - Informix: clidriver for Linux and macOS

Status: todo
Depends on: P21-106

## Goal
An Informix profile opens in the Avalonia head on Linux and macOS, with the platform's `clidriver` shipped and found without any global install.

Exists today: `Squill.Data.Informix` resolves only the Windows `db2cli64.dll` (`ClidriverLocator`, `OdbcNative.Library`); `eng/Clidriver.targets` copies the Windows folder for `win-x64` publishes only. On the other platforms an Informix profile fails with `DllNotFoundException`. The platform packages are `Net.IBM.Data.Db2-lnx` and `Net.IBM.Data.Db2-osx` (same version family as `Net.IBM.Data.Db2` 10.0.0.400, check the exact versions and layout first).

## Design decisions (final)
- Restore the packages in `eng/Clidriver.targets` per runtime identifier (`linux-x64`, `osx-arm64`, `osx-x64`), copy the whole `clidriver` folder (same reason as on Windows: `REDIST.txt` is incomplete), with the licence files.
- The resolver gives the platform library name (`libdb2.so`, `libdb2.dylib`) from the folder, by explicit path (no `LD_LIBRARY_PATH`, no `PATH`); `SQUILL_CLIDRIVER` still wins. Check whether the runtime libraries need `dlopen` flags or a loader path for their siblings, and whether a global setting is needed: if it is, stop and report (global installs are not approved).
- Check the C runtime locale behaviour (`setlocale` in the driver, see the P21-106 Done notes) on each platform; the restore is Windows only today.
- Packaging for the Avalonia head (archive or package format of the Linux and macOS builds): see `docs/release.md`; the licence ships as files (P21-110 may add a screen).

## Out of scope
- Windows ARM64, the WinUI head.

## Tasks
- [ ] Per-platform resolver tests (pure, folder layouts in a temp directory).
- [ ] Targets and publish check for each runtime identifier available on the build machine; `not run (<why>)` for the others.
- [ ] Docker test on Linux (the Podman machine) if the test project can run there; otherwise a documented manual run.
- [ ] Docs: README row (Windows only becomes the real platform list), ADR 0008 line, `docs/poc/informix-driver.md`.

## Acceptance criteria
- Recipe 2 acceptance; `-Engine informix` green on at least Linux; the Avalonia AOT check has baseline warnings only.

## Risks
- Unverified: the packages were never opened in this repository; sizes and layout may differ from Windows.

## Suggested commit message
Ship the Informix clidriver on Linux and macOS
