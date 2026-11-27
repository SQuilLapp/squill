# P21-09 - Oracle: end-to-end verification in both heads and the support matrix

Status: todo
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
- [ ] Automated: run every Oracle Docker-gated suite, record counts.
- [ ] Drive and capture (recipe 3); fill the agent column.
- [ ] Docs as above, `CHANGELOG.md` line.

## Acceptance criteria
- Recipe 2 and 3 acceptance; docs updated; every undriven step written `not driven (<why>)`.

## Risks
- Image size (`gvenzl/oracle-free:23-slim-faststart`, about 2 GB): run it alone.

## Open points (defaults)
- Owner decides whether the bridge's .NET runtime requirement is acceptable for a release (default: yes, documented).

## Suggested commit message
Verify Oracle end to end and list it as a supported engine
