# P19-08 - One script that prepares the data of the verification checklists

Status: todo
Depends on: none

## Goal
The "How to prepare" section of `docs/verification/new-screens.md` asks the owner (and the agent cards) to hand-build: several profiles of different environments with a populated recent list (M01), two SQLite files with differences (M09), a CSV and an empty SQLite table (M10). `tools/visual/New-VisualProfile.ps1` only creates one profile (`pg-prod-eu`). Each person repeats the setup and gets different data; a checklist result cannot be compared. Prepare it once, deterministically, so the owner's ticking starts at step 1.

## Design decisions (final)
- Extend `tools/visual/New-VisualProfile.ps1` with `-Scenario Basic|Full` (default `Basic` = today's behavior, bit for bit). `Full` adds, in the same `-Dir`:
  - six SQLite profiles with the four environments (`Production`, `Staging`, `Development`, `Local`: read `src/Squill.Core/ConnectionEnvironment.cs` and the profile fields in `src/Squill.Core`) named like the mockup M01 and the palette tests (for example `pg-prod-eu`, `mysql-staging`, `sqlserver-dev`, `local-sqlite`), files `<Dir>/<name>.db` each built from `seed.sql`;
  - `recent.json` in the format of `JsonRecentConnectionsStore` (`src/Squill.Platform/Profiles`, card P18-01), newest first, so the home page lists the six with different last-opened times (generate the timestamps relative to now);
  - `cmp_source.db` and `cmp_target.db` (from `seed.sql`, then SQL that gives one added table, one removed table, one table with an extra column, one index only on one side, one view that differs) and two profiles for them (`cmp-source` Staging, `cmp-target` Production);
  - `people.csv` (UTF-8, `;` separator, a header, 40 rows with accents, a quoted field containing the separator, three rows that fail conversion: text in an integer column, an impossible date, an empty required field) and `import_target.db` with an empty `people` table (an integer key, text, date, numeric) plus profile `import-target`;
  - `settings.json` only when `-Theme Light|Dark` is given (format in `tools/visual/README.md`, section Theme), default none.
- Uses the `sqlite3` CLI when present, otherwise `Seed.cs` as today; for the extra SQL pass a second `.sql` file kept next to `seed.sql` (`scenario.sql`, one block per database, readable by both paths). Idempotent: re-running deletes and recreates every file it owns, never other files in `-Dir`.
- Prints a summary table: file, profile, environment, purpose (M01, M09, M10). Windows only, like the other scripts.
- Update `tools/visual/README.md` and the "How to prepare" lines of `docs/verification/new-screens.md` (the SQLite bullet and the start commands) to use `-Scenario Full`. Do not touch any checklist row or tick.

## Out of scope
- PostgreSQL / MySQL / SQL Server data (the `-Keep` containers of `eng/run-docker-tests.ps1` stay), Avalonia or WinUI code.

## Tasks
- [ ] Script, `scenario.sql`, README updates.
- [ ] Verify: run `-Scenario Full -Dir <tmp>` twice (second run identical, no error); start the Avalonia head with `SQUILL_DATA_DIR=<tmp>` and read the home page with a capture (P19-07 if merged, else by looking at the window): six cards with environment pills and distinct times. Open compare with the two compare profiles and check that the differences listed above show (counts). Open the import wizard on `import-target` with `people.csv`: the preview shows the accents and the quoted field. Describe what was seen in the Done notes; close the app.
- [ ] Confirm `-Scenario Basic` output is unchanged (diff of `profiles.json` against a run of the old script from git).

## Acceptance criteria
- The scenario creates exactly the files above; both heads read the profiles (the store format is the app's, not a copy); processes closed, `<tmp>` removed.

## Risks
- The recent store format is the app's: read the store class before writing JSON by hand and prefer the real field names; if the format has a version field, set it.
- `ConnectionEnvironment` names must match the enum JSON form the store serializes (string vs number): copy a profile saved by the app.

## Suggested commit message
Prepare the data of the verification checklists with one scenario script
