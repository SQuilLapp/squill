# P1-10 - Data.Postgres: schema reading

Status: done
Depends on: P1-09

## Goal
Implement `ReadSchemaAsync` for PostgreSQL.

## Design decisions (final)
- `DefaultSchema` from `select current_schema()`. Exclude schemas `pg_catalog`, `information_schema`, `pg_toast`, and names starting with `pg_temp`/`pg_toast_temp`.
- Tables, views and materialized views (materialized as `TableKind.View`) from `pg_class`/`pg_namespace` (`relkind in ('r','p','v','m')`); columns from `pg_attribute` with `format_type(atttypid, atttypmod)` as `DataType`, `pg_get_expr(adbin, adrelid)` as default; PKs and indexes from `pg_index`/`pg_get_indexdef`-free column lists via `pg_attribute`; FKs from `pg_constraint` (`contype = 'f'`).
- One round trip per object kind (tables+columns, indexes, FKs), grouped in memory, not one query per table.

## Out of scope
- Functions, sequences, triggers.

## Tasks
- [x] Implement `PostgresSchemaReader` (internal).
- [x] Docker-gated tests with a schema containing PK, composite index, FK across schemas, view, default expression.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add PostgreSQL schema reading
