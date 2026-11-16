# P16-08 - Documentation: SSH host keys ADR 0013, changelog correction and README

Status: todo
Depends on: P16-01 ... P16-07

## Goal
Record what phase 16 changed and correct documentation that is wrong or missing since 0.3.0.

## Design decisions (final)
- `docs/adr/0013-ssh-host-keys.md` (format of ADR 0012): context (every key was trusted), decision (trust on first use, store `known-hosts.json`, fingerprint `SHA256:`, changed key refused and never replaced, unknown key refused when nobody can be asked), the dialog rules (`Annuler` default, `Entrée` cancels), consequences, known limits (no "forget host key" command: the user edits the file named in the error; the user's OpenSSH `known_hosts` is not read; host certificates unsupported), not done.
- `CHANGELOG.md`: under `## [Unreleased]` add entries (format rules at the top of the file): Added (SSH host key question and refusal of a changed key; designer warning for SQLite triggers that use a renamed or dropped column), Fixed (diagnostics no longer slow typing in long scripts; formatter `TOP`/`WITH TIES`; `END$$` delimiter split). **Correct the `0.3.0` entry**: it says production confirmation applies to "a Production or Staging connection", but `ProductionGuard` only asks on Production and ADR 0012 lists a `Staging` confirmation as not done; fix the sentence to Production only (verify in `src/Squill.Presentation/Safety/ProductionGuard.cs` first and say in the report what was found). Do not touch `eng/Version.props`.
- `README.md` and `docs/architecture.md`: mention the table filter ("Choose tables", `ITableFilterStore`), the cell preview pane, the cell pickers and `DEFAULT`, the single-instance rule (`SingleInstance`), the Oracle proof of concept status as it really is (read `docs/poc/oracle-bridge.md` and `docs/adr/0008-engine-parity.md`, do not overstate), the known-hosts store and `IKnownHostsStore`; each only where a feature list or layer list already exists, no new sections. Add the phase 16 status line to `docs/tasks/README.md`.
- Verify each claim against the code with a grep before writing it.

## Out of scope
- New features, ticking the owner's manual checklists.

## Tasks
- [ ] ADR, changelog, README, architecture, task index status.

## Acceptance criteria
- Build and test green (docs only, but run them); every link in the touched files resolves (grep the new file names); `eng/release-notes.ps1` still extracts the `Unreleased` and `0.3.0` sections (run it for both).

## Suggested commit message
Document the SSH host key check (ADR 0013) and correct the 0.3.0 changelog
