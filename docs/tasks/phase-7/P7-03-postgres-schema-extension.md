# P7-03 - Data.Postgres: read auto-increment, foreign-key actions and key name

Status: done
Depends on: P7-01, P1-10

## Goal
`PostgresSchemaReader` fills the new model members.

## Design decisions (final)
- `IsAutoIncrement` is true when `pg_attribute.attidentity <> ''` (identity column; `DefaultExpression` stays `null`) or when the default expression starts with `nextval(` (serial; `DefaultExpression` is kept as reported).
- Foreign-key actions come from `pg_constraint.confdeltype` and `confupdtype`: `a` NoAction, `r` Restrict, `c` Cascade, `n` SetNull, `d` SetDefault.
- `PrimaryKeyName` is the `conname` of the `contype = 'p'` constraint of the table, `null` when there is none.
- Extend the existing queries; do not add a round trip per table.

## Out of scope
- Other engines.

## Tasks
- [x] Extend the reader.
- [x] Extend the Docker-gated tests (`SQUILL_DOCKER_TESTS=1`, as in P1-10): identity column, `serial` column, each action, a named primary key (`CONSTRAINT pk_x PRIMARY KEY`) and the default `<table>_pkey` name.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Read auto-increment, foreign-key actions and key name from PostgreSQL
