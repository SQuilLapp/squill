# P11-40 - Documentation: engine parity ADR 0008, README and architecture note

Status: done
Depends on: P11-39, P11-38, P11-37, P11-36

## Goal
Record what phase 11 decided and what each engine supports, so the next phase (new engines) starts from facts.

## Design decisions (final)
- `docs/adr/0008-engine-parity.md` (same layout as ADR 0007): context, decisions (Podman runner and CI containers, connection options, multiple result sets, schema objects scope, database listing, column type policy), consequences, and the **support matrix** table feature x engine (SQLite, PostgreSQL, MySQL, SQL Server) with `full`, `partial (reason)`, `no (reason)`, built from the four verification reports. Explicitly list what is not supported (sequences, user-defined types, synonyms, events, spatial types, Windows authentication, MariaDB, Azure SQL) and the Avalonia head status (still on AvaloniaEdit, see ADR 0002; heads' verification results).
- `README.md`: supported engines table with a link to the ADR matrix, testing section linking `docs/testing.md`. `docs/architecture.md`: "Order of work" line for phase 11 and a short "Engine parity (phase 11)" paragraph like the phase 9/10 ones. `docs/tasks/README.md`: statuses of phase 11 set to done only for cards actually done.
- Open findings and the cards created during phase 11 (`P11-41` onwards) are listed under "Open items" in the ADR.

## Out of scope
- New code.

## Tasks
- [x] Write the three documents, check every link with a grep for the file names.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` 0 warnings (docs only, sanity); all relative links resolve.

## Suggested commit message
Document engine parity: ADR 0008, support matrix, README and architecture note
