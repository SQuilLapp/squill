# P18-33 - Schema compare between two MySQL databases

Status: todo
Depends on: P18-05, P18-06, P18-08, P18-31

## Goal
On MySQL the schema of a table is its database name (`MySqlSchemaReader.cs:104`: `new SchemaSnapshot(database, tables)`). `SchemaComparer` puts the schema in the match key (`Key(TableInfo)` and the routine `Key`), so `squill.cmp_a` (source) and `squill_cmp_target.cmp_a` (target) never match: every common table shows as one Added plus one Removed, and the generated script would drop and recreate it. Found by P18-31: `SchemaCompareFlowTests.CompareAndRunTheScript_LeavesNothingToCompare` in `tests/Squill.Data.MySql.Tests` fails (the other 157 MySQL tests pass). PostgreSQL, SQL Server and SQLite compare inside equal schema names and are not affected.

## Design decisions (final)
- Rule, in `Squill.Core` (`SchemaComparer`): when the two snapshots have different `DefaultSchema` values (case-insensitive; this is the cross-database case, only MySQL today), a schema equal to its own snapshot's `DefaultSchema` counts as "the default schema" on both sides. Compute a canonical schema per snapshot (`null` for its default schema, the real name otherwise) and use it in every match key: tables and views (`Key`), routines, and `SameForeignKey` (`ReferencedSchema`, canonicalized with the snapshot the foreign key belongs to). When both defaults are equal (PostgreSQL `public`, SQL Server `dbo`, SQLite `main`) nothing changes: keep the current behavior bit for bit.
- `ObjectDifference.Schema` of a default-schema object is `null` in the cross-database case (the UI and `ObjectKey` already accept a null schema), so one key identifies the object on both sides.
- `MigrationScriptBuilder` (`src/Squill.Sql/Migration/`): its lookups (`FindRelation`, `FindRoutine`, the `Eq(t.Schema, x.Schema)` comparisons in the index and drop paths, the foreign key dependency check) must resolve a null schema as the default schema of the snapshot they search, and `Q(schema, name)` must write an unqualified name for it. **Never write the source database name into the script**: the script runs on the target connection, and `squill.cmp_a` there would alter the source database. For a non-default schema keep today's qualification.
- Put the "is this the default schema of that snapshot" logic in one place (an internal helper next to `SchemaSnapshot` or in `Squill.Core/Schema/Compare/`; make it public only if `Squill.Sql` needs it and has no `InternalsVisibleTo`: then XML doc and the CA rules apply). No reflection, no `NoWarn`.
- Out of scope: comparing across dialects, view bodies that hard-code the database name (a view whose definition mentions `squill.` still differs textually: record it as a known limit in the Done notes and in `docs/verification/new-screens.md`), the Oracle reader.

## Tasks
- [ ] `SchemaComparer` canonical schema as above; results stay in a deterministic order (the `Compare` sort uses `Schema`; null sorts first).
- [ ] `MigrationScriptBuilder` lookups and qualification as above.
- [ ] Tests in `tests/Squill.Core.Tests/Schema/SchemaComparerTests.cs`: two snapshots with `DefaultSchema` `a` and `b` holding the same table, extra column on the source, give one Modified (not Added + Removed); a table only in the source is one Added with a null schema; a foreign key that references the table by each side's own database name compares equal; a routine with the same signature compares equal; identical defaults (`public` / `public`) behave as before (existing tests unchanged and green); a non-default second schema keeps its name.
- [ ] Tests in `tests/Squill.Sql.Tests/Migration/MigrationScriptBuilderTests.cs` (MySQL dialect): the script for that Modified table is an `ALTER TABLE` with an unqualified name; no statement contains either database name; a target-only table becomes an unqualified `DROP TABLE`.
- [ ] Docker proof: `rtk pwsh eng/run-docker-tests.ps1 -Engine mysql` (Podman, see `docs/tasks/phase-11/verification-recipe.md`; if the VM reports `io_setup() failed with EAGAIN`, raise `fs.aio-max-nr` to 1048576 inside it, as noted in `docs/verification/new-screens.md`). `SchemaCompareFlowTests.CompareAndRunTheScript_LeavesNothingToCompare` must pass. Also run the same script for `postgres`, `sqlserver` and the SQLite compare flow test to prove nothing changed.
- [ ] Update `docs/verification/new-screens.md`: MySQL line of "Automated" (158 passed), the "Bugs found" entry marked fixed, the M09 row (drop "bug -> P18-33").

## Acceptance criteria
- Common acceptance (recipe 7, `phase-18/mockups-recipe.md`); the report separates "automated, ran" from "not driven". Every container and process closed (`podman ps -a` empty, `dotnet build-server shutdown`).
- Only MySQL changes behavior; the new unit tests above exist and pass.

## Risks
- A script that qualifies with the source database name would silently modify the wrong database: the unqualified-name test is mandatory.
- Foreign keys and routines are easy to forget: the Docker scenario has neither, so only the new unit tests protect them.
- Database names are case sensitive on Linux MySQL; keep the ordinal-ignore-case comparison used everywhere in the comparer.

## Suggested commit message
Compare MySQL databases by their default schema instead of the database name
