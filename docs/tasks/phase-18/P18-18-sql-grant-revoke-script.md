# P18-18 - Sql: GRANT and REVOKE script

Status: todo
Depends on: P18-16

## Goal
The `SQL GÉNÉRÉ` panel of M11: the statements for a `PrivilegeChangeSet`. Recipe 1.

## Design decisions (final)
- `src/Squill.Sql/Security/PostgresPrivilegeScript.cs`: `Build(string role, PrivilegeChangeSet changes, string connectionName)` returns `PrivilegeScript(string Text, IReadOnlyList<string> Statements)`. Header comment `-- Rôle <role> . <connection>` (italic in the view), then per (object, change kind) one statement merging the kinds: `GRANT SELECT, INSERT ON public.stocks TO analyste;`, `REVOKE DELETE ON analytics.ventes_jour FROM analyste;`. Order: grants then revokes grouped by object, objects in name order; deterministic. Object keyword: `ON TABLE` is optional in PostgreSQL and works for views; emit the bare `ON <schema>.<name>` form as M11 does. Identifiers quoted by `SqlQuoting` (the sample role `analyste` stays unquoted because it needs no quoting).
- `Statements` is the list the runner executes one by one inside a transaction (`BEGIN` / `COMMIT` are not in the text; the Presentation card brackets them through `SessionTransaction`).
- An empty change set gives empty text.

## Out of scope
- Execution, `WITH GRANT OPTION`, `ALTER DEFAULT PRIVILEGES`, other dialects.

## Tasks
- [ ] Builder.
- [ ] Tests in `tests/Squill.Sql.Tests/Security/`: the M11 example (one grant, one revoke) golden text, merged kinds, quoting of a mixed-case role and a schema with a dash, ordering, empty set, determinism.
- [ ] Docker-gated proof in the PostgreSQL test project: apply a built script, re-read privileges with P18-17, the state matches.

## Acceptance criteria
- Common acceptance (recipe 7), no head touched.

## Suggested commit message
Add the privilege GRANT and REVOKE script builder
