# P7-27 - Documentation: schema designer ADR, README and architecture note

Status: todo
Depends on: P7-24, P7-26, P6-04

## Goal
Record the design decisions of phase 7 and document the feature for users.

## Design decisions (final)
- New `docs/adr/0004-schema-designer.md` in the format of the existing ADRs (read `0003-sql-parser.md`): context, decision, consequences. It records: the draft-versus-original model with `OriginalName` for rename detection; abstract `SchemaOperation`s produced by `TableDiff` and translated by hand-written per-dialect generators (no ORM, no reflection); SQL Server default constraints looked up at run time; SQLite changes beyond its native `ALTER TABLE` done by table rebuild inside a transaction with `foreign_keys` off; MySQL non-transactional DDL and the warning shown to the user; layered ER layout (deterministic, pure, routed edges that may cross boxes); per-profile layout persistence; what is not covered (views, triggers, check constraints, partitions, moving tables between schemas, comments, collations).
- `README.md`: a short "Schema designer and ER diagram" section (what it does, the supported dialects, the SQLite rebuild and MySQL notes), matching the existing README tone; do not restructure the file.
- `docs/architecture.md`: update "Order of work" to say phase 7 is done and link the ADR; add nothing else.
- No code changes.

## Out of scope
- New features, screenshots that cannot be produced in the session.

## Tasks
- [ ] Write the ADR, update the README and architecture note, check every link.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings (no code touched; confirms the tree is still green).
- All relative links in the changed documents resolve.

## Suggested commit message
Document the schema designer and ER diagram
