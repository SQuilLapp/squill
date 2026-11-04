# P11-16 - Data.Sqlite: triggers and view definitions, SQLite sample schema

Status: done
Depends on: P11-15

## Goal
The SQLite reader fills `TableInfo.Definition` for views and `SchemaSnapshot.Triggers`, and a SQLite sample schema exists for the later end-to-end cards.

## Design decisions (final)
- Source: `select type, name, tbl_name, sql from sqlite_master where type in ('view','trigger')`. View `Definition` = the `SELECT` part after `AS` of the stored `CREATE VIEW` (keep the full statement when it cannot be split safely; document). Trigger: `Squill.Data.Sqlite` must not reference `Squill.Sql`, so `Timing` and `Events` come from a small case-insensitive scan of the stored statement (`BEFORE|AFTER|INSTEAD OF`, then `INSERT|UPDATE|DELETE` joined with `, `); `Definition` = the full `CREATE TRIGGER` text.
- Check what the reader does today with virtual and shadow tables (`sqlite_`-prefixed, FTS shadow tables) and attached databases; report and fix if system tables leak into the tree.
- `tests/Shared/SampleSchema/sqlite.sql`: same objects as the server samples (customers, orders, order_items with composite key, a view, an index, FKs with each action, a `WITHOUT ROWID` table, a rowid-only table without a primary key, one table with every storage class and declared types `INTEGER PRIMARY KEY AUTOINCREMENT`, `NUMERIC(10,2)`, `BLOB`, `DATETIME`, `BOOLEAN`, `TEXT`, plus a trigger). Test `SampleSchema_LoadsAndIsReadBack` as in P11-02, never skipped.

## Out of scope
- Routines (SQLite has none).

## Tasks
- [x] Reader changes, sample file, tests (view definition, trigger fields, no internal tables).

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` 0 warnings; `rtk bypass dotnet test --solution Squill.slnx -c Release` green.

## Suggested commit message
Read SQLite triggers and view definitions, add the SQLite sample schema
