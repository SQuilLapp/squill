# P21-98 - MariaDB: SQL corpus JSON operators and the TLS test premise

Status: todo
Depends on: P21-01

## Goal
Two shared MySQL tests fail on `mariadb:11.4` for reasons that are not product bugs: `SqlCorpusTests` contains `j -> '$.a'` and `->>` (MariaDB has no such operators, only `json_extract`), and `OpenAsync_TlsVerifyFull_RejectsSelfSignedCertificate` expects a self-signed server certificate while the MariaDB container serves no TLS. Make both tests explicit about the engine: split the corpus statement into a MySQL-only list, and start the MariaDB container with a self-signed certificate (`--ssl-cert`, `--ssl-key`) or skip the TLS test with a stated reason.

## Design decisions (final)
- No product code change unless the formatter or completion rejects a statement MariaDB accepts.
- A skipped test names its reason; it is never counted as verified.

## Out of scope
- Plans (P21-02), the activity reader (P21-97).

## Tasks
- [ ] Split the corpus; give the container a certificate or skip the TLS test with a reason.

## Acceptance criteria
- Recipe 2 common acceptance; `rtk pwsh eng/run-docker-tests.ps1 -Engine mariadb` shows no failure except P21-02 and P21-97 ones.

## Suggested commit message
Make the MySQL corpus and TLS tests engine-aware for MariaDB
