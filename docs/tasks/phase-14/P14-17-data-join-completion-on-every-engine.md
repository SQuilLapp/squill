# P14-17 - Data: generated join conditions run on every engine

Status: done
Depends on: P14-08, P14-09, P14-11

## Goal
Prove against real servers that the text the popup generates is valid on each engine and joins the right rows: schema read by the real reader, completion computed by the real providers, the resulting statement executed.

## Design decisions (final)
- Shared scenario `tests/Shared/JoinCompletionScenario.cs` (namespace `Squill.Data.RoundTrip`, pattern of `PlanScenario` / `PlanSpec`: a `JoinCompletionSpec(DatabaseDialect Dialect, string Prefix, string IntType, string ScriptName)` record carries the per-engine spelling; the sample schema of `tests/Shared/SampleSchema/<engine>.sql` supplies `customers`, `orders` (single key to customers) and `order_items` (composite primary key, key to orders); copy the loading code of `PlanScenarioTests`). Scratch tables for the cases the sample lacks are created by the scenario itself in the sample schema (SQLite: main) and dropped in `finally`: `cx_person (id, boss_id references cx_person)` (self reference), `cx_trip (id, from_city_id, to_city_id)` and `cx_city (id)` with two keys from `cx_trip` to `cx_city`, `cx_pair_a (k1, k2, primary key (k1, k2))` and `cx_pair_b (a1, a2, foreign key (a1, a2) references cx_pair_a (k1, k2))` (composite key). Per-engine type spelling in the spec only.
- Steps, per engine, through `connection.ReadSchemaAsync` then `new CompositeCompletionProvider(new KeywordCompletionProvider(), new SchemaCompletionProvider()).GetCompletions(new SqlRequest(text, offset, dialect, schema))`: (1) `select * from <p>customers c join |` first row inserts to a statement that **executes** and returns the seeded rows (seed as `PlanScenario.SeedAsync` does: 3 customers, 3 orders: count 3); (2) the same for `left join`, `right join` (where the engine has it: not covered if SQLite < 3.39, check `sqlite_version()`; MySQL `full join` is absent, assert the keyword provider does not offer it), `full join` (not MySQL): every kind gives **the same condition text** and executes; (3) `order_items` after `orders o` gives the composite condition and executes; (4) the repeat / self join `from <p>cx_person e join |` gives two rows with alias, both execute; (5) two keys `cx_trip t join |` give two rows for `cx_city`, both execute and differ in the condition; (6) composite key between `cx_pair_a` and `cx_pair_b` executes; (7) quoting: a table named with a space and a capital (`"Odd Name"` / `[Odd Name]` / `` `Odd Name` ``, scratch table `cx_odd` with column `Odd Id` referenced by a key from `cx_pair_c`) is inserted quoted and executes; (8) `cross join` and `natural join` positions produce no join rows; (9) after typing `on ` by hand the proposed conditions (P14-09) also execute.
- A step fails the test with the generated statement in the message. The tests never write assertion text that depends on server-specific error wording.
- One test class per driver project (`tests/Squill.Data.<Driver>.Tests/Completion/JoinCompletionScenarioTests.cs`, pattern of `Plans/PlanScenarioTests.cs`, Docker-gated except SQLite, which always runs). The Data test projects must be able to reference `Squill.Sql` (check the csproj; if a project does not, add the `ProjectReference` to its test csproj only, never to a `src` project; `Squill.Data` must not depend on `Squill.Sql` per the dependency graph, tests may).

## Out of scope
- Oracle, performance, the UI.

## Tasks
- [x] Scenario and the four test classes; run `rtk pwsh eng/run-docker-tests.ps1 -Engine all` (Podman; see [verification-recipe.md](../phase-11/verification-recipe.md) section 1; stop and report if Podman is unavailable, never mark skipped tests as verified); SQLite tests run without Docker.
- [x] A bug found by the scenario in a Sql card: fix it here when under ~30 lines with a regression test in `tests/Squill.Sql.Tests`, otherwise write card `P14-20` onwards (add it to the index).

## Acceptance criteria
- Recipe section 4; Docker run for all engines green with nothing skipped, `podman ps -a` shows no container left, `dotnet build-server shutdown` done.

## Suggested commit message
Prove generated join conditions on the four engines

## Report
- Bug found by step (1) on PostgreSQL and fixed here (3 lines + regression test): `TableReference.NeedsSchema` treated a table with a unique name as resolvable bare even when the engine has a default schema, so `sample.orders` was inserted as `orders` (relation "orders" does not exist). A bare name resolves only in the default schema; the unique-name fallback now applies only when `DefaultSchema` is null (SQLite, MySQL without a database). `TableReferenceTests.UniqueNameInOtherSchema` now expects `audit.log` (it encoded the wrong rule from P14-03).
- Result: SQLite (no Docker), PostgreSQL 17, MySQL 8.4, SQL Server 2022 all green via `eng/run-docker-tests.ps1`, nothing skipped. Two MySQL full runs had unrelated fixture start failures ("container state improper", 1 then 4 tests); the next full MySQL run was green and the class itself passed alone each time (infrastructure flake, not investigated).
- Steps run: join, every kind (inner, left, left outer, right, full; MySQL without full, asserted absent), child table, self join (2 rows), two keys (2 rows), composite key (`AND`), quoted `Odd Name`, no row after cross / natural, conditions after `on `. A control statement against a missing table proves errors surface as exceptions.
