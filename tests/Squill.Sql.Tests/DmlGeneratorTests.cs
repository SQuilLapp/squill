using Squill.Core;
using Squill.Core.Editing;
using Squill.Sql.Editing;
using Xunit;

namespace Squill.Sql.Tests;

public sealed class DmlGeneratorTests
{
    private static ColumnDefinition Def(string name, bool pk = false) => new(name, "int", !pk, pk, null);

    private static EditTarget Target(string? schema, string table, string[] keys, params string[] cols)
    {
        var defs = cols.Select(c => Def(c, keys.Contains(c))).ToArray();
        return new EditTarget(schema, table,
            [.. defs.Select((d, i) => new EditColumn(i, d))], keys,
            new TableInfo(schema ?? "", table, TableKind.Table, defs, [], []));
    }

    private static readonly EditTarget Users = Target("main", "Users", ["Id"], "Id", "Name", "Age");
    private static readonly EditTarget Pairs = Target(null, "Pairs", ["A", "B"], "A", "B", "C");
    private static readonly EditTarget Odd = Target("s]\"`", "t]\"`", ["k]\"`"], "k]\"`", "v");

    private static ColumnValue V(string c, object? v) => new(c, v);

    public static TheoryData<DatabaseDialect, string> QualifiedUsers => new()
    {
        { DatabaseDialect.Sqlite, "\"main\".\"Users\"" },
        { DatabaseDialect.PostgreSql, "\"main\".\"Users\"" },
        { DatabaseDialect.MySql, "`main`.`Users`" },
        { DatabaseDialect.SqlServer, "[main].[Users]" },
    };

    public static TheoryData<DatabaseDialect> Dialects => new()
    {
        DatabaseDialect.Sqlite, DatabaseDialect.PostgreSql, DatabaseDialect.MySql, DatabaseDialect.SqlServer,
    };

    private static string Q(DatabaseDialect d, string id) => d switch
    {
        DatabaseDialect.MySql => $"`{id}`",
        DatabaseDialect.SqlServer => $"[{id}]",
        _ => $"\"{id}\"",
    };

    [Fact]
    public void Update_WritesTheDefaultKeyword_AndInsertOmitsTheColumn()
    {
        var update = DmlGenerator.Generate(new UpdateRow([V("Id", 1)], [V("Name", SqlDefault.Value), V("Age", 3)]), Users, DatabaseDialect.PostgreSql);
        var insert = DmlGenerator.Generate(new InsertRow([V("Id", 1), V("Name", SqlDefault.Value)]), Users, DatabaseDialect.PostgreSql);

        Assert.Equal("UPDATE \"main\".\"Users\" SET \"Name\" = DEFAULT, \"Age\" = @p0 WHERE \"Id\" = @p1", update.Sql);
        Assert.Equal("INSERT INTO \"main\".\"Users\" (\"Id\") VALUES (@p0)", insert.Sql);
    }

    [Fact]
    public void Update_OnSqliteWritesTheDefaultExpression()
    {
        var defs = new[] { Def("Id", true), new ColumnDefinition("Name", "text", true, false, "'x'") };
        var target = new EditTarget("main", "Users", [.. defs.Select((d, i) => new EditColumn(i, d))], ["Id"], new TableInfo("main", "Users", TableKind.Table, defs, [], []));

        var sql = DmlGenerator.Generate(new UpdateRow([V("Id", 1)], [V("Name", SqlDefault.Value)]), target, DatabaseDialect.Sqlite).Sql;

        Assert.Equal("UPDATE \"main\".\"Users\" SET \"Name\" = 'x' WHERE \"Id\" = @p0", sql);
    }

    [Theory]
    [InlineData(DatabaseDialect.PostgreSql, "json", "INSERT INTO \"main\".\"Users\" (\"Id\") VALUES (@p0::json)")]
    [InlineData(DatabaseDialect.PostgreSql, "jsonb", "INSERT INTO \"main\".\"Users\" (\"Id\") VALUES (@p0::jsonb)")]
    [InlineData(DatabaseDialect.MySql, "json", "INSERT INTO `main`.`Users` (`Id`) VALUES (@p0)")]
    [InlineData(DatabaseDialect.SqlServer, "xml", "INSERT INTO [main].[Users] ([Id]) VALUES (@p0)")]
    public void JsonParametersAreCastOnPostgresOnly(DatabaseDialect dialect, string dataType, string expected)
    {
        var def = new ColumnDefinition("Id", dataType, true, true, null);
        var target = new EditTarget("main", "Users", [new EditColumn(0, def)], ["Id"], new TableInfo("main", "Users", TableKind.Table, [def], [], []));
        Assert.Equal(expected, DmlGenerator.Generate(new InsertRow([V("Id", "{}")]), target, dialect).Sql);
        var update = DmlGenerator.Generate(new UpdateRow([V("Id", 1)], [V("Id", "{}")]), target, dialect).Sql;
        Assert.Contains(dialect == DatabaseDialect.PostgreSql ? $"= @p0::{dataType} WHERE" : "= @p0 WHERE", update);
    }

    [Theory, MemberData(nameof(QualifiedUsers))]
    public void Insert_WithValues(DatabaseDialect d, string table)
    {
        var s = DmlGenerator.Generate(new InsertRow([V("Name", "Ann"), V("Age", null)]), Users, d);
        Assert.Equal($"INSERT INTO {table} ({Q(d, "Name")}, {Q(d, "Age")}) VALUES (@p0, @p1)", s.Sql);
        Assert.Equal([new DmlParameter("p0", "Ann"), new DmlParameter("p1", null)], s.Parameters);
    }

    [Theory]
    [InlineData(DatabaseDialect.Sqlite, "INSERT INTO \"main\".\"Users\" DEFAULT VALUES")]
    [InlineData(DatabaseDialect.PostgreSql, "INSERT INTO \"main\".\"Users\" DEFAULT VALUES")]
    [InlineData(DatabaseDialect.SqlServer, "INSERT INTO [main].[Users] DEFAULT VALUES")]
    [InlineData(DatabaseDialect.MySql, "INSERT INTO `main`.`Users` () VALUES ()")]
    public void Insert_NoValues(DatabaseDialect d, string expected)
    {
        var s = DmlGenerator.Generate(new InsertRow([]), Users, d);
        Assert.Equal(expected, s.Sql);
        Assert.Empty(s.Parameters);
    }

    [Theory, MemberData(nameof(QualifiedUsers))]
    public void Update_OneAndSeveralColumns(DatabaseDialect d, string table)
    {
        var one = DmlGenerator.Generate(new UpdateRow([V("Id", 7)], [V("Name", "Bob")]), Users, d);
        Assert.Equal($"UPDATE {table} SET {Q(d, "Name")} = @p0 WHERE {Q(d, "Id")} = @p1", one.Sql);
        Assert.Equal([new DmlParameter("p0", "Bob"), new DmlParameter("p1", 7)], one.Parameters);

        var many = DmlGenerator.Generate(new UpdateRow([V("Id", 7)], [V("Name", "Bob"), V("Age", 3)]), Users, d);
        Assert.Equal($"UPDATE {table} SET {Q(d, "Name")} = @p0, {Q(d, "Age")} = @p1 WHERE {Q(d, "Id")} = @p2", many.Sql);
        Assert.Equal(3, many.Parameters.Count);
    }

    [Theory, MemberData(nameof(Dialects))]
    public void Update_OfKeyColumn_UsesOriginalKeyInWhere(DatabaseDialect d)
    {
        var s = DmlGenerator.Generate(new UpdateRow([V("Id", 1)], [V("Id", 2)]), Users, d);
        Assert.Contains($"SET {Q(d, "Id")} = @p0 WHERE {Q(d, "Id")} = @p1", s.Sql);
        Assert.Equal([new DmlParameter("p0", 2), new DmlParameter("p1", 1)], s.Parameters);
    }

    [Theory, MemberData(nameof(Dialects))]
    public void CompositeKey_AndNullKeyValue(DatabaseDialect d)
    {
        var s = DmlGenerator.Generate(new DeleteRow([V("A", 1), V("B", null)]), Pairs, d);
        Assert.Equal($"DELETE FROM {Q(d, "Pairs")} WHERE {Q(d, "A")} = @p0 AND {Q(d, "B")} IS NULL", s.Sql);
        Assert.Equal([new DmlParameter("p0", 1)], s.Parameters);

        var u = DmlGenerator.Generate(new UpdateRow([V("A", 1), V("B", 2)], [V("C", 9)]), Pairs, d);
        Assert.Equal($"UPDATE {Q(d, "Pairs")} SET {Q(d, "C")} = @p0 WHERE {Q(d, "A")} = @p1 AND {Q(d, "B")} = @p2", u.Sql);
    }

    [Theory, MemberData(nameof(QualifiedUsers))]
    public void Delete(DatabaseDialect d, string table)
    {
        var s = DmlGenerator.Generate(new DeleteRow([V("Id", 4)]), Users, d);
        Assert.Equal($"DELETE FROM {table} WHERE {Q(d, "Id")} = @p0", s.Sql);
    }

    [Theory]
    [InlineData(DatabaseDialect.Sqlite, "\"s]\"\"`\".\"t]\"\"`\"", "\"v\"", "\"k]\"\"`\"")]
    [InlineData(DatabaseDialect.PostgreSql, "\"s]\"\"`\".\"t]\"\"`\"", "\"v\"", "\"k]\"\"`\"")]
    [InlineData(DatabaseDialect.MySql, "`s]\"```.`t]\"```", "`v`", "`k]\"```")]
    [InlineData(DatabaseDialect.SqlServer, "[s]]\"`].[t]]\"`]", "[v]", "[k]]\"`]")]
    public void Identifiers_AreQuotedWithEmbeddedQuotes(DatabaseDialect d, string table, string v, string k)
    {
        var s = DmlGenerator.Generate(new UpdateRow([V("k]\"`", 1)], [V("v", 2)]), Odd, d);
        Assert.Equal($"UPDATE {table} SET {v} = @p0 WHERE {k} = @p1", s.Sql);
    }

    [Fact]
    public void Invalid_Edits_Throw()
    {
        const DatabaseDialect d = DatabaseDialect.Sqlite;
        Assert.Throws<ArgumentException>(() => DmlGenerator.Generate(new DeleteRow([]), Users, d));
        Assert.Throws<ArgumentException>(() => DmlGenerator.Generate(new DeleteRow([V("Name", 1)]), Users, d));
        Assert.Throws<ArgumentException>(() => DmlGenerator.Generate(new UpdateRow([V("B", 1), V("A", 1)], [V("C", 1)]), Pairs, d));
        Assert.Throws<ArgumentException>(() => DmlGenerator.Generate(new UpdateRow([V("A", 1)], [V("C", 1)]), Pairs, d));
        Assert.Throws<ArgumentException>(() => DmlGenerator.Generate(new UpdateRow([V("Id", 1)], []), Users, d));
        Assert.Throws<ArgumentException>(() => DmlGenerator.Generate(new UpdateRow([V("Id", 1)], [V("Nope", 1)]), Users, d));
        Assert.Throws<ArgumentException>(() => DmlGenerator.Generate(new InsertRow([V("Nope", 1)]), Users, d));
    }

    [Theory]
    [InlineData(DatabaseDialect.PostgreSql, "character(5)", "WHERE \"K\" = @p1::bpchar")]
    [InlineData(DatabaseDialect.PostgreSql, "character varying(5)", "WHERE \"K\" = @p1")]
    [InlineData(DatabaseDialect.SqlServer, "char(5)", "WHERE [K] = @p1")]
    public void KeyOfPaddedCharType_PostgresComparesAsBpchar(DatabaseDialect d, string dataType, string expected)
    {
        var key = new ColumnDefinition("K", dataType, false, true, null);
        var v = new ColumnDefinition("V", "int", true, false, null);
        var target = new EditTarget(null, "t", [new EditColumn(0, key), new EditColumn(1, v)], ["K"], new TableInfo("", "t", TableKind.Table, [key, v], [], []));
        var sql = DmlGenerator.Generate(new UpdateRow([V("K", "ab   ")], [V("V", 1)]), target, d).Sql;
        Assert.Contains(expected, sql.Replace("\"K\"", d == DatabaseDialect.PostgreSql ? "\"K\"" : "[K]"));
    }
}
