# P21-09 - Oracle: end-to-end verification in both heads and the support matrix

Status: done
Depends on: P21-03 ... P21-08

## Goal
Drive Oracle in the published apps and promote it from "proof of concept" to a supported engine in the docs.

Exists today: the verification reports for four engines (`docs/verification/*.md`), ADR 0008 matrix, README "Oracle exists only as a proof of concept".

## Design decisions (final)
- New `docs/verification/oracle.md` in the format of `postgres.md` (automated section, owner checklist with empty boxes; agent writes `agent: ok / bug / not driven`).
- Drive the WinUI AOT exe (then an Avalonia smoke) on the Oracle container: connect, explorer, run, cancel, manual transaction, grid edit, designer create/alter, ER, plan, terminal.
- Bridge packaging check: the published `oracle-bridge/` folder is present and the error when the .NET runtime is missing is readable.
- Docs: ADR 0008 gets an Oracle column; README "Supported databases" gets an Oracle row (driver `Oracle.ManagedDataAccess.Core` through `Squill.OracleBridge`, verdict "bridge process, .NET runtime required"); ADR 0012 says Oracle has manual transactions with implicit DDL commit; `docs/poc/oracle-bridge.md` keeps the protocol only.

## Out of scope
- New features found missing: a card `P21-97` onwards each.

## Tasks
- [x] Automated: run every Oracle Docker-gated suite, record counts.
- [x] Drive and capture (recipe 3); fill the agent column (partial, see Done notes).
- [x] Docs as above, `CHANGELOG.md` line.

## Acceptance criteria
- Recipe 2 and 3 acceptance; docs updated; every undriven step written `not driven (<why>)`.

## Risks
- Image size (`gvenzl/oracle-free:23-slim-faststart`, about 2 GB): run it alone.

## Open points (defaults)
- Owner decides whether the bridge's .NET runtime requirement is acceptable for a release (default: yes, documented).

## Suggested commit message
Verify Oracle end to end and list it as a supported engine

## Done notes
- Premise check: the bridge folder, ADR 0008 paragraph and README text were as described; the ADR 0012 sentence said Oracle has no probe, but `OracleDatabaseConnection` implements `ITransactionProbe` since P21-03 (fixed in the ADR). Report: [docs/verification/oracle.md](../../verification/oracle.md).
- Automated: `-Engine oracle` 114 of 114 green. Both heads AOT-publish with the four baseline warnings only.
- Drive: WinUI published exe and an Avalonia smoke on Oracle Free (see the report). Not driven: grid edit, designer, terminal (no `sqlplus`), TCPS, the real missing-runtime start of the app.
- Fixed here: Oracle plan errors used the reason code `engine`, so the window showed the generic text instead of the missing `SELECT_CATALOG_ROLE` (`PlanUnavailableException` code `detail`, `PlanUnavailableTextTests`); a bridge that exits at once now reports its stderr (missing .NET runtime) in the error (`BridgeExitTests`).
- New bug cards: P21-100 (killed bridge leaves a dead session), P21-101 (empty password gives "Value cannot be null").
- Docs: ADR 0008 Oracle column, ADR 0012, README (supported table, plans, transactions), `CHANGELOG.md`, `docs/poc/oracle-bridge.md` title.
