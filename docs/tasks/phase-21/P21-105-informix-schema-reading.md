# P21-105 - Data.Informix: schema reading (tables, columns, keys, indexes, routines, triggers, views)

Status: done
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
- [x] Pure tests first: `Decode` over every code (`CHAR`, `SMALLINT`, `INTEGER`, `FLOAT`, `SMALLFLOAT`, `DECIMAL`, `SERIAL`, `DATE`, `MONEY`, `DATETIME`, `BYTE`, `TEXT`, `VARCHAR`, `INTERVAL`, `NCHAR`, `NVARCHAR`, `INT8`, `SERIAL8`, `LVARCHAR`, `BOOLEAN`, `BIGINT`, `BIGSERIAL`, `BLOB`, `CLOB`), the nullability bit, the row-to-snapshot mapping against canned rows (fake connection).
- [x] Docker test: a sample schema (two tables with a foreign key, a unique index, a view, a procedure, a trigger, one column of each decoded type) and a snapshot assertion.
- [x] Done notes: what the catalogue does not give (for example index sort order) and how the reader handles it.

## Acceptance criteria
- Recipe 2 common acceptance; `-Engine informix` green.

## Risks
- `syscolumns.coltype` encodes nullability and type in one number; decoding errors show as wrong types, hence the table-driven tests.
- Routine and trigger bodies are stored in chunks (`sysprocbody.data`, ordered by `seqno`): join in order.

## Suggested commit message
Read the Informix schema from the system catalogue

## Done notes
- Shipped (in `src/Squill.Data.Informix`): `InformixColumnType` (`Decode(coltype, collength, extendedId)`, `IsNotNull`, `IsSerial`) and `InformixSchemaReader`, wired into `InformixDatabaseConnection.ReadSchemaAsync`. Tests: 35 decode rows and one canned-catalogue mapping test (`InformixSchemaReaderTests`, fake `IDatabaseConnection`), one Docker test (`InformixSchemaTests`, new database `sqschema` in the fixture) with every type of the card; 104/104 in `-Engine informix`, 4725 passed / 399 skipped in the whole solution.
- Premise check: the card said "follow `SqlServerSchemaReader`"; the shape actually followed is `OracleSchemaReader` (a static reader over `IDatabaseConnection`, plain catalogue SQL). Both slnx builds clean with `-warnaserror`.
- Schema choice: the schema of a table is its `owner` (trimmed), `DefaultSchema` is `user`; names are kept as stored (lower case).
- Measured catalogue facts: user routines are the `sysprocedures` rows with an upper-case `mode` (`O`, `D`, `R`, `T`; the 572 built-ins have `d`), not a `procid` range; `trim()` fails on the `LVARCHAR` column `paramtypes` (trimmed in the reader instead); `primary` is usable unquoted in `sysreferences`; a table can have only one `SERIAL`/`SERIAL8`/`BIGSERIAL` column (the sample schema uses three tables); `sysprocbody.data` (`datakey 'T'`) and `sysviews.viewtext` are `CHAR` chunks joined unchanged in `seqno` order, then trimmed; `systrigbody` has the header under `datakey 'D'` and the triggered actions (with `BEFORE`/`AFTER`) under `'A'`, the other keys are compiled code.
- What the catalogue does not give, and how it is handled: index sort order (the sign of `partN` gives `DESC`, but `IndexInfo` has no field: the column order is kept, the sign dropped); `ON UPDATE` (Informix has none: `NoAction`); `RESTRICT` vs `NO ACTION` (only `delrule 'C'` is read, everything else is `NoAction`); trigger timing (derived from the first `BEFORE`/`AFTER`/`INSTEAD OF` of the action text); a function's return type (parsed from `RETURNING` in the text, null when the pattern does not match); column defaults (`sysdefaults` not read: `DefaultExpression` is null, left for the designer cards).
- The primary-key and foreign-key backing indexes are not listed as indexes (like the Oracle reader); a unique constraint is listed as a unique index under the constraint name.
- Not run: Native AOT publish (P21-106); `SET`/`MULTISET`/`LIST`/`ROW`/named opaque types decode to their bare name or `unknown(<code>)`, not tested on a server; `INTERVAL` leading precision other than the sample was only unit-tested. `Error_ReportsTheSqlState_AndServerVersionIsRead` failed once in an earlier run of this card and passed on the rerun (not investigated, no code of this card involved).
