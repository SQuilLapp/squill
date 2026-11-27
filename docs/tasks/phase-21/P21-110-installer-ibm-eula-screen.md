# P21-110 - Installer: a real EULA screen for the IBM clidriver licence

Status: todo
Depends on: P21-106

## Goal
The Windows installer shows IBM's licence text and requires acceptance before it installs the `clidriver` folder, instead of only shipping the text as files.

Exists today: `eng/Clidriver.targets` copies `clidriver/license/*` (`Lic_en.txt`, `REDIST.txt`, `odbc_REDIST.txt`, notices) beside the exe. Velopack `vpk pack` (the setup `.exe`) has no EULA screen; only its `--msi` output has one (a licence file, `--msi`-only options). See `docs/release.md` and `eng/` for how packs are built.

## Design decisions (final)
- Start by checking in `docs/release.md` and the pack script what `--msi` needs (licence in RTF, extra tooling) and whether the auto-update path (which does not use an MSI) still works. If the MSI cannot carry the licence and the update flow together, stop with a report: the alternative is a first-run acceptance dialog in the app, shown once before the first Informix connection (state kept in `install-state.json`), with the same IBM text from `clidriver/license/Lic_en.txt`.
- The text shown is IBM's file, unchanged.

## Out of scope
- Other drivers, changing the update engine.

## Tasks
- [ ] Decide MSI screen or first-use dialog with a short written check of both against `docs/release.md`.
- [ ] Implement the smaller one; both catalogs for any new UI text.
- [ ] Test the acceptance state (a pure test) and drive the installer or the dialog once.
- [ ] Document it in `docs/release.md` and the licence note of `docs/poc/informix-driver.md`.

## Acceptance criteria
- A clean install cannot reach a connected Informix session without the licence having been shown and accepted; the build is warning free.

## Risks
- An MSI changes how updates are delivered; a dialog in the app is the safer fallback.

## Suggested commit message
Show the IBM licence before the first Informix connection
