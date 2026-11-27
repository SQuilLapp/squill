# P21-11 - Data.Informix: provider (connect, execute, stream, schema reading)

Status: todo
Depends on: P21-10

## Goal
A `DatabaseDialect.Informix` profile opens, runs statements, streams results and fills the explorer.

**This card is written for the default verdict of P21-10 (in-process `Net.IBM.Data.Db2` over DRDA). If P21-10 concludes "bridge" or "own ODBC layer", rewrite the Design decisions first (stop rule).**

Exists today: nothing for Informix. Patterns: `Squill.Data.Sqlite` ... `Squill.Data.SqlServer` (provider, connection, result reader, schema reader), `Squill.Data.Oracle` (bridge variant).

## Design decisions (final)
- `DatabaseDialect.Informix` (Core), `src/Squill.Data.Informix` (+ `tests/Squill.Data.Informix.Tests`), registered in both heads' composition roots; connection editor tile and engine icon in both heads (small: copy the Oracle tile).
- Profile: host, port (default 9089 DRDA), database, user; server name not needed over DRDA.
- Parameters: `?` positional is what the driver binds; the query tab rewrites `:name` to positional in order (a new `ParameterScanner` mode for Informix, tested).
- Schema: `systables` (`tabtype` T/V, `tabid >= 100`), `syscolumns` (decode `coltype` and `collength` into type text, nullability bit 256), `sysconstraints` + `sysindexes` for PK/FK/indexes, `sysreferences` for FK targets, `sysprocedures`, `systriggers`, `sysviews` for definitions.
- Not built here: designer DDL, grid editing, plans, transactions probe, terminal (`dbaccess`) - each a later card if wanted (list them in the card's Done notes as `P21-97+` proposals).

## Out of scope
- SQL dialect (P21-12).

## Tasks
- [ ] Unit tests of the type decoding and schema mapping (pure).
- [ ] Docker-gated tests on `-Engine informix`: connect, run, stream 10,000 rows, cancel, schema snapshot of a sample schema.
- [ ] AOT probe re-run with the provider linked into the WinUI head; zero new warnings.
- [ ] README "Supported databases" row with the measured verdict.

## Acceptance criteria
- Recipe 2 common acceptance; `-Engine informix` green; WinUI AOT publish baseline only.

## Risks
- `syscolumns.coltype` encodes nullability and type in one number; decoding errors show as wrong types: table-driven tests over every type of the sample schema.

## Open points (defaults)
- None beyond P21-10.

## Suggested commit message
Add the Informix provider with schema reading
