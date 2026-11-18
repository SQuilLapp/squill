using System.Text.RegularExpressions;
using Squill.Core;
using Squill.Core.Schema.Compare;
using Squill.Sql.Migration;
using Xunit;

namespace Squill.Sql.Tests.Migration;

public sealed class MigrationScriptBuilderTests
{
    private static readonly MigrationOptions Options = new("dev", "prod", new DateTime(2026, 5, 6, 7, 8, 0));

    private static readonly RoutineInfo Calc = new("public", "calc", RoutineKind.Function, "calc(numeric)", "numeric", "CREATE OR REPLACE FUNCTION public.calc(numeric) RETURNS numeric AS $$ select 1 $$ LANGUAGE sql");

    private static ColumnDefinition Col(string name, string type, bool nullable = true, bool pk = false) => new(name, type, nullable, pk, null);

    private static TableInfo Table(string name, IReadOnlyList<ColumnDefinition> cols, IReadOnlyList<IndexInfo>? indexes = null, IReadOnlyList<ForeignKeyInfo>? fks = null, string? schema = "public") =>
        new(schema, name, TableKind.Table, cols, indexes ?? [], fks ?? []);

    private static TableInfo View(string name, string? definition, TableKind kind = TableKind.View, string? schema = "public") =>
        new(schema, name, kind, [Col("id", "integer")], [], [], null, definition);

    private static SchemaSnapshot Snap(string? schema, IReadOnlyList<TableInfo> tables, IReadOnlyList<RoutineInfo>? routines = null) =>
        new(schema, tables) { Routines = routines ?? [] };

    private static MigrationScript Run(DatabaseDialect d, SchemaSnapshot s, SchemaSnapshot t, MigrationOptions? o = null, Func<ObjectDifference, bool>? include = null)
    {
        var comparison = SchemaComparer.Compare(s, t);
        var set = comparison.Differences.Where(include ?? (x => x.Kind != DifferenceKind.Removed)).Select(ObjectKey.Of).ToHashSet();
        return MigrationScriptBuilder.Build(d, s, t, comparison, set, o ?? Options);
    }

    [Fact]
    public void Postgres_AddedModifiedAndRemovedObjects_GoldenText()
    {
        var parent = Table("parent", [Col("id", "integer", false, true)]);
        var source = Snap("public",
        [
            parent,
            Table("child", [Col("id", "integer", false, true), Col("parent_id", "integer"), Col("note", "text")],
                [new IndexInfo("ix_parent", false, ["parent_id"])], [new ForeignKeyInfo("fk_p", ["parent_id"], "public", "parent", ["id"])]),
            View("v", "SELECT 1 AS id;"),
        ]);
        var target = Snap("public",
        [
            parent with { Columns = [Col("id", "integer", false, true), Col("gone", "text")] },
            View("v", "SELECT 2 AS id"),
            Table("old", [Col("id", "integer")]),
        ], [Calc]);

        var script = Run(DatabaseDialect.PostgreSql, source, target);
        Assert.Equal(
            """
            -- dev -> prod . generated 2026-05-06 07:08

            BEGIN;

            -- DROP FUNCTION "public".calc(numeric);  (excluded)

            -- DROP TABLE "public"."old";  (excluded)

            CREATE TABLE "public"."child" (
                "id" integer NOT NULL,
                "parent_id" integer,
                "note" text,
                PRIMARY KEY ("id"),
                CONSTRAINT "fk_p" FOREIGN KEY ("parent_id") REFERENCES "public"."parent" ("id") ON DELETE NO ACTION ON UPDATE NO ACTION
            );

            CREATE INDEX "ix_parent" ON "public"."child" ("parent_id");

            ALTER TABLE "public"."parent" DROP COLUMN "gone";

            CREATE OR REPLACE VIEW "public"."v" AS
            SELECT 1 AS id;

            COMMIT;
            """.ReplaceLineEndings("\n"),
            script.Text);
        Assert.Equal(3, script.ChangeCount);
        Assert.Contains(new MigrationNote(MigrationNoteKind.DropsData, "parent.gone"), script.Notes);
    }

    [Fact]
    public void RemovedObjectsIncluded_AreDroppedFirst_WithNotes()
    {
        var source = Snap("public", [Table("keep", [Col("id", "integer")])]);
        var target = Snap("public", [Table("keep", [Col("id", "integer")]), Table("old", [Col("id", "integer")]), View("ov", "SELECT 1")], [Calc]);
        var script = Run(DatabaseDialect.PostgreSql, source, target, include: _ => true);
        var text = script.Text;
        Assert.True(text.IndexOf("DROP VIEW", StringComparison.Ordinal) < text.IndexOf("DROP FUNCTION", StringComparison.Ordinal));
        Assert.True(text.IndexOf("DROP FUNCTION", StringComparison.Ordinal) < text.IndexOf("DROP TABLE", StringComparison.Ordinal));
        Assert.DoesNotContain("excluded", text, StringComparison.Ordinal);
        Assert.Equal(3, script.ChangeCount);
        Assert.Equal([new MigrationNote(MigrationNoteKind.DropsData, "old")], script.Notes);
    }

    [Fact]
    public void ForeignKeyBetweenNewTables_ReferencedTableIsCreatedFirst()
    {
        var a = Table("a_child", [Col("id", "integer", false, true), Col("p", "integer")], fks: [new ForeignKeyInfo("fk", ["p"], "public", "z_parent", ["id"])]);
        var z = Table("z_parent", [Col("id", "integer", false, true)]);
        var text = Run(DatabaseDialect.PostgreSql, Snap("public", [a, z]), Snap("public", [])).Text;
        Assert.True(text.IndexOf("CREATE TABLE \"public\".\"z_parent\"", StringComparison.Ordinal) < text.IndexOf("CREATE TABLE \"public\".\"a_child\"", StringComparison.Ordinal));
    }

    [Fact]
    public void ForeignKeyCycle_AddsTheKeyAfterBothCreates()
    {
        var a = Table("a", [Col("id", "integer", false, true), Col("b", "integer")], fks: [new ForeignKeyInfo("fk_ab", ["b"], "public", "b", ["id"])]);
        var b = Table("b", [Col("id", "integer", false, true), Col("a", "integer")], fks: [new ForeignKeyInfo("fk_ba", ["a"], "public", "a", ["id"])]);
        var text = Run(DatabaseDialect.PostgreSql, Snap("public", [a, b]), Snap("public", [])).Text;
        var alter = text.IndexOf("ALTER TABLE \"public\".\"a\" ADD CONSTRAINT \"fk_ab\"", StringComparison.Ordinal);
        Assert.True(alter > text.IndexOf("CREATE TABLE \"public\".\"b\"", StringComparison.Ordinal));
        Assert.Contains("CONSTRAINT \"fk_ba\"", text, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(text, "fk_ab"));
    }

    [Fact]
    public void StandaloneIndexes_AreEmittedOnlyWhenTheTableIsNotAltered()
    {
        var plain = new[] { Col("id", "integer"), Col("x", "integer") };
        var source = Snap("public", [Table("t", plain, [new IndexInfo("ix_x", false, ["x"])])]);
        var target = Snap("public", [Table("t", plain)]);
        var comparison = SchemaComparer.Compare(source, target);
        var indexOnly = comparison.Differences.Where(x => x.Category == ObjectCategory.Index).Select(ObjectKey.Of).ToHashSet();
        var text = MigrationScriptBuilder.Build(DatabaseDialect.PostgreSql, source, target, comparison, indexOnly, Options).Text;
        Assert.Contains("CREATE INDEX \"ix_x\" ON \"public\".\"t\" (\"x\")", text, StringComparison.Ordinal);

        var all = comparison.Differences.Select(ObjectKey.Of).ToHashSet();
        var both = MigrationScriptBuilder.Build(DatabaseDialect.PostgreSql, source, target, comparison, all, Options).Text;
        Assert.Single(Regex.Matches(both, "CREATE INDEX"));
    }

    [Theory]
    [InlineData(DatabaseDialect.PostgreSql, "BEGIN;", "COMMIT;")]
    [InlineData(DatabaseDialect.Sqlite, "BEGIN;", "COMMIT;")]
    [InlineData(DatabaseDialect.SqlServer, "BEGIN TRANSACTION;", "COMMIT;")]
    public void Wrapper_ByDialect(DatabaseDialect d, string begin, string commit)
    {
        var source = Snap("s", [Table("t", [Col("id", "int")], schema: "s")]);
        var text = Run(d, source, Snap("s", [])).Text;
        Assert.Contains($"\n\n{begin}\n\n", text, StringComparison.Ordinal);
        Assert.EndsWith($"\n\n{commit}", text, StringComparison.Ordinal);
        var plain = Run(d, source, Snap("s", []), Options with { WrapInTransaction = false }).Text;
        Assert.DoesNotContain(begin, plain, StringComparison.Ordinal);
        Assert.DoesNotContain("COMMIT", plain, StringComparison.Ordinal);
    }

    [Fact]
    public void MySql_HasNoWrapperAndNotesTheImplicitCommit()
    {
        var script = Run(DatabaseDialect.MySql, Snap(null, [Table("t", [Col("id", "int")], schema: null)]), Snap(null, []));
        Assert.DoesNotContain("BEGIN", script.Text, StringComparison.Ordinal);
        Assert.Contains("-- MySQL commits DDL implicitly", script.Text, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE `t`", script.Text, StringComparison.Ordinal);
        Assert.Contains(new MigrationNote(MigrationNoteKind.MySqlImplicitCommit, ""), script.Notes);
    }

    [Fact]
    public void Oracle_IsNotSupported() =>
        Assert.Throws<NotSupportedException>(() => MigrationScriptBuilder.Build(DatabaseDialect.Oracle, SchemaSnapshot.Empty, SchemaSnapshot.Empty, new SchemaComparison([]), new HashSet<ObjectKey>(), Options));

    [Fact]
    public void NothingIncluded_WritesOnlyTheHeader()
    {
        var script = MigrationScriptBuilder.Build(DatabaseDialect.PostgreSql, SchemaSnapshot.Empty, SchemaSnapshot.Empty, new SchemaComparison([]), new HashSet<ObjectKey>(), Options);
        Assert.Equal("-- dev -> prod . generated 2026-05-06 07:08\n\n-- no change selected", script.Text);
        Assert.Equal(0, script.ChangeCount);
    }

    [Fact]
    public void Views_ByDialect()
    {
        var source = Snap("s", [View("v", "SELECT 1", schema: "s")]);
        var target = Snap("s", [View("v", "SELECT 2", schema: "s")]);
        Assert.Contains("CREATE OR REPLACE VIEW \"s\".\"v\" AS\nSELECT 1", Run(DatabaseDialect.PostgreSql, source, target).Text, StringComparison.Ordinal);
        Assert.Contains("CREATE OR REPLACE VIEW `s`.`v` AS", Run(DatabaseDialect.MySql, source, target).Text, StringComparison.Ordinal);
        var sqlite = Run(DatabaseDialect.Sqlite, source, target).Text;
        Assert.Contains("DROP VIEW \"s\".\"v\";\n\nCREATE VIEW \"s\".\"v\" AS\nSELECT 1;", sqlite, StringComparison.Ordinal);

        var mssqlSource = Snap("s", [View("v", "CREATE VIEW [s].[v] AS SELECT 1", schema: "s")]);
        var mssqlTarget = Snap("s", [View("v", "CREATE VIEW [s].[v] AS SELECT 2", schema: "s")]);
        Assert.Contains("CREATE OR ALTER VIEW [s].[v] AS SELECT 1;", Run(DatabaseDialect.SqlServer, mssqlSource, mssqlTarget).Text, StringComparison.Ordinal);
    }

    [Fact]
    public void MaterializedView_IsDroppedAndCreated()
    {
        var source = Snap("public", [View("m", "SELECT 1", TableKind.MaterializedView)]);
        var target = Snap("public", [View("m", "SELECT 2", TableKind.MaterializedView)]);
        Assert.Contains("DROP MATERIALIZED VIEW \"public\".\"m\";\n\nCREATE MATERIALIZED VIEW \"public\".\"m\" AS\nSELECT 1;", Run(DatabaseDialect.PostgreSql, source, target).Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Routines_ByDialectAndUnknownDefinition()
    {
        var changed = Calc with { Definition = Calc.Definition + " -- v2" };
        Assert.Contains("CREATE OR REPLACE FUNCTION public.calc", Run(DatabaseDialect.PostgreSql, Snap("public", [], [changed]), Snap("public", [], [Calc])).Text, StringComparison.Ordinal);

        var my = new RoutineInfo("db", "f", RoutineKind.Function, "f(IN a int)", "int", "RETURN a");
        var myText = Run(DatabaseDialect.MySql, Snap("db", [], [my with { Definition = "RETURN a + 1" }]), Snap("db", [], [my])).Text;
        Assert.Contains("DROP FUNCTION `db`.`f`;\n\nCREATE FUNCTION `f`(IN a int)\nRETURNS int\nRETURN a + 1;", myText, StringComparison.Ordinal);

        var ms = new RoutineInfo("dbo", "p", RoutineKind.Procedure, "p", null, "CREATE PROCEDURE dbo.p AS SELECT 1");
        Assert.Contains("CREATE OR ALTER PROCEDURE dbo.p AS SELECT 1;", Run(DatabaseDialect.SqlServer, Snap("dbo", [], [ms]), Snap("dbo", [])).Text, StringComparison.Ordinal);

        var hidden = Calc with { Definition = null };
        Assert.Contains("-- definition not readable: calc(numeric)", Run(DatabaseDialect.PostgreSql, Snap("public", [], [hidden]), Snap("public", [])).Text, StringComparison.Ordinal);
    }

    [Fact]
    public void NarrowingColumn_IsNoted_WideningIsNot()
    {
        static TableInfo T(string type) => Table("t", [Col("id", "integer", false, true), Col("c", type)]);
        Assert.Contains(new MigrationNote(MigrationNoteKind.NarrowsColumn, "t.c"), Run(DatabaseDialect.PostgreSql, Snap("public", [T("varchar(10)")]), Snap("public", [T("varchar(20)")])).Notes);
        Assert.Contains(new MigrationNote(MigrationNoteKind.NarrowsColumn, "t.c"), Run(DatabaseDialect.PostgreSql, Snap("public", [T("integer")]), Snap("public", [T("text")])).Notes);
        Assert.DoesNotContain(MigrationNoteKind.NarrowsColumn, Run(DatabaseDialect.PostgreSql, Snap("public", [T("varchar(30)")]), Snap("public", [T("varchar(20)")])).Notes.Select(n => n.Kind));
    }

    [Fact]
    public void Sqlite_Rebuild_KeepsPragmasOutsideTheTransaction()
    {
        static TableInfo T(string type) => Table("t", [Col("id", "INTEGER", false, true), Col("c", type)], schema: null);
        var script = Run(DatabaseDialect.Sqlite, Snap(null, [T("TEXT")]), Snap(null, [T("INTEGER")]));
        var text = script.Text;
        Assert.Contains(new MigrationNote(MigrationNoteKind.SqliteRebuild, "t"), script.Notes);
        Assert.True(text.IndexOf("PRAGMA foreign_keys = OFF;", StringComparison.Ordinal) < text.IndexOf("BEGIN;", StringComparison.Ordinal));
        Assert.True(text.IndexOf("COMMIT;", StringComparison.Ordinal) < text.IndexOf("PRAGMA foreign_key_check;", StringComparison.Ordinal));
        Assert.EndsWith("PRAGMA foreign_keys = ON;", text, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(text, "PRAGMA foreign_keys = OFF"));
    }

    [Fact]
    public void Output_IsDeterministic()
    {
        var source = Snap("public", [Table("b", [Col("id", "integer")]), Table("a", [Col("id", "integer")])]);
        Assert.Equal(Run(DatabaseDialect.PostgreSql, source, Snap("public", [])).Text, Run(DatabaseDialect.PostgreSql, source, Snap("public", [])).Text);
    }

    [Fact]
    public void MySql_TwoDatabases_ScriptIsUnqualifiedAndNamesNeitherDatabase()
    {
        var source = Snap("src_db", [Table("t", [Col("id", "int", false, true), Col("x", "int")], schema: "src_db")]);
        var target = Snap("dst_db", [Table("t", [Col("id", "int", false, true)], schema: "dst_db"), Table("old", [Col("id", "int")], schema: "dst_db")]);
        var text = Run(DatabaseDialect.MySql, source, target, include: _ => true).Text;
        Assert.Contains("ALTER TABLE `t`", text, StringComparison.Ordinal);
        Assert.Contains("DROP TABLE `old`", text, StringComparison.Ordinal);
        Assert.DoesNotContain("src_db", text, StringComparison.Ordinal);
        Assert.DoesNotContain("dst_db", text, StringComparison.Ordinal);
    }
}
