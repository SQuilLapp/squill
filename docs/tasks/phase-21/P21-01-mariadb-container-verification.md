# P21-01 - MariaDB: container tests and the dialect differences

Status: todo
Depends on: none

## Goal
Make MariaDB a verified engine instead of "not tested, not a goal" ([ADR 0008](../../adr/0008-engine-parity.md)).

Exists today: `ConnectionProfile.MariaDb` (bool, MySQL dialect through `MySqlConnector`), a MariaDB tile and engine icon in both connection editors (`ConnectionEditorWindow`, `EngineIcon`, `ConnectionEditorFormat`). Nothing ran against a MariaDB server. Gap: a container, the shared engine scenarios run on it, and the differences fixed or recorded.

## Design decisions (final)
- Image `mariadb:11.4` (LTS) through Testcontainers, new `-Engine mariadb` in `eng/run-docker-tests.ps1` and in the CI job (P11-05).
- Reuse the MySQL Docker-gated test classes by parameterizing their fixture (`MySqlFixture` gains a MariaDB variant); no copy of the tests.
- Known differences to check and handle in `Squill.Data.MySql` and `Squill.Sql` (MySQL dialect, `MariaDb` flag passed where needed): `JSON` is an alias of `LONGTEXT` with a `CHECK (json_valid())`; `RETURNING` on `INSERT`/`DELETE` (not used yet: record only); sequences (not supported anywhere: record only); `information_schema` column differences (`GENERATION_EXPRESSION`, `COLUMN_DEFAULT` quoting: MariaDB returns `'text'` with quotes and `NULL` as the text `NULL`); `CHECK` constraints listed in `information_schema.CHECK_CONSTRAINTS`.
- A difference fixable in one small edit is fixed with a regression test; a bigger one becomes a card `P21-97` onwards (index row added by this card's commit).

## Out of scope
- Plans and the transaction probe (P21-02). Galera, MaxScale, MariaDB-only types (`INET6`, `UUID`) beyond reading them as text.

## Tasks
- [ ] Add the MariaDB fixture variant and the `-Engine mariadb` option; run the MySQL suites on it.
- [ ] Fix or record each failure (column defaults, JSON column kind, schema reading, DML round trip, designer DDL, grid commit, completion corpus).
- [ ] Update `docs/column-types.md` with a MariaDB column where it differs, `docs/testing.md` with the engine.

## Acceptance criteria
- Recipe 2 common acceptance; `rtk pwsh eng/run-docker-tests.ps1 -Engine mariadb` green (or each remaining failure turned into a `P21-97+` card).

## Risks
- `MySqlConnector` server version detection: some code may branch on `ServerVersion` and treat MariaDB 11 as MySQL 11; check every `ServerVersion` use.

## Open points (defaults)
- MariaDB version: 11.4 LTS only (default); 10.6 LTS added later if the owner needs it.

## Suggested commit message
Run the MySQL engine suites on a MariaDB container and fix the differences
