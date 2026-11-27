# P21-105 - Data.Informix: schema reading (tables, columns, keys, indexes, routines, triggers, views)

Status: todo
Depends on: P21-104

## Goal
An Informix schema reader fills the explorer snapshot from the Informix system catalogue through the connection of P21-104 (plain SQL, no `SQLTables`).

Exists today: the P21-104 connection; the schema readers of the other providers (`SqlServerSchemaReader`, `OracleSchemaReader`) and the shared snapshot model. Read them first and follow the same shape.

## Design decisions (final)
- `systables` (`tabtype` T table, V view; `tabid >= 100` skips system tables; `owner`), `syscolumns` (decode `coltype` and `collength` into the type text: bit 256 of `coltype` = NOT NULL, `coltype mod 256` = type code, `DECIMAL` precision and scale from `collength`, `DATETIME` / `INTERVAL` qualifiers from `collength`, `VARCHAR` / `LVARCHAR` lengths), `sysconstraints` + `sysindexes` for primary keys, unique constraints and indexes (the `part1` ... `part16` columns), `sysreferences` for foreign key targets, `sysprocedures` (+ `sysprocbody`), `systriggers` (+ `systrigbody`), `sysviews` for view definitions.
- The type decoding is a pure table-driven function (`InformixColumnType.Decode(coltype, collength, extendedId)`) with a test row for every type of the sample schema; unknown codes give `unknown(<code>)`, never an exception.
- Names are lower-case as stored; quoting follows the dialect of P21-12 (until then the generic quoter).
- Informix has no schemas in the PostgreSQL sense (owner only): the snapshot has one schema level named after the owner, or the database when the shared model needs one; record the choice in the Done notes.

## Out of scope
- Designer DDL, grid editing, plans, registration in the app (P21-106), SQL dialect (P21-12).

## Tasks
- [ ] Pure tests first: `Decode` over every code (`CHAR`, `SMALLINT`, `INTEGER`, `FLOAT`, `SMALLFLOAT`, `DECIMAL`, `SERIAL`, `DATE`, `MONEY`, `DATETIME`, `BYTE`, `TEXT`, `VARCHAR`, `INTERVAL`, `NCHAR`, `NVARCHAR`, `INT8`, `SERIAL8`, `LVARCHAR`, `BOOLEAN`, `BIGINT`, `BIGSERIAL`, `BLOB`, `CLOB`), the nullability bit, the row-to-snapshot mapping against canned rows (fake connection).
- [ ] Docker test: a sample schema (two tables with a foreign key, a unique index, a view, a procedure, a trigger, one column of each decoded type) and a snapshot assertion.
- [ ] Done notes: what the catalogue does not give (for example index sort order) and how the reader handles it.

## Acceptance criteria
- Recipe 2 common acceptance; `-Engine informix` green.

## Risks
- `syscolumns.coltype` encodes nullability and type in one number; decoding errors show as wrong types, hence the table-driven tests.
- Routine and trigger bodies are stored in chunks (`sysprocbody.data`, ordered by `seqno`): join in order.

## Suggested commit message
Read the Informix schema from the system catalogue
