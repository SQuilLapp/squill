using Squill.Core;
using Squill.Core.Editing;
using Squill.Sql.Editing;
using Xunit;

namespace Squill.Sql.Tests;

public sealed class InsertPlanTests
{
    private static readonly string[] ColumnNames = ["Id", "Id2", "Name"];

    private static EditTarget Target(params string[] keys)
    {
        var defs = ColumnNames
            .Select(c => new ColumnDefinition(c, "int", true, keys.Contains(c), null, keys.Contains(c))).ToArray();
        return new EditTarget("main", "T", [.. defs.Select((d, i) => new EditColumn(i, d))], keys,
            new TableInfo("main", "T", TableKind.Table, defs, [], []));
    }

    private static ColumnValue V(string c, object? v) => new(c, v);

    [Theory]
    [InlineData(DatabaseDialect.Sqlite, "INSERT INTO \"main\".\"T\" (\"Name\") VALUES (@p0) RETURNING \"Id\"", null)]
    [InlineData(DatabaseDialect.PostgreSql, "INSERT INTO \"main\".\"T\" (\"Name\") VALUES (@p0) RETURNING \"Id\"", null)]
    [InlineData(DatabaseDialect.SqlServer, "INSERT INTO [main].[T] ([Name]) OUTPUT INSERTED.[Id] VALUES (@p0)", null)]
    [InlineData(DatabaseDialect.MySql, "INSERT INTO `main`.`T` (`Name`) VALUES (@p0)", "SELECT LAST_INSERT_ID()")]
    public void GeneratedKey(DatabaseDialect d, string sql, string? keyQuery)
    {
        var plan = DmlGenerator.PlanInsert(new InsertRow([V("Name", "a")]), Target("Id"), d);
        Assert.Equal(sql, plan.Insert.Sql);
        Assert.Equal(keyQuery, plan.KeyQuery?.Sql);
        Assert.Equal(d != DatabaseDialect.MySql, DmlGenerator.ReturnsKeyFromInsert(d));
    }

    [Theory]
    [InlineData(DatabaseDialect.Sqlite)]
    [InlineData(DatabaseDialect.PostgreSql)]
    [InlineData(DatabaseDialect.MySql)]
    [InlineData(DatabaseDialect.SqlServer)]
    public void ExplicitKey_NoReadBack(DatabaseDialect d)
    {
        var edit = new InsertRow([V("Id", 5), V("Name", "a")]);
        var plan = DmlGenerator.PlanInsert(edit, Target("Id"), d);
        Assert.Null(plan.KeyQuery);
        Assert.Equal(DmlGenerator.Generate(edit, Target("Id"), d).Sql, plan.Insert.Sql);
    }

    [Fact]
    public void NoValues_SqlServerOutputBeforeDefaultValues()
    {
        var plan = DmlGenerator.PlanInsert(new InsertRow([]), Target("Id"), DatabaseDialect.SqlServer);
        Assert.Equal("INSERT INTO [main].[T] OUTPUT INSERTED.[Id] DEFAULT VALUES", plan.Insert.Sql);
    }

    [Fact]
    public void NoValues_PostgresReturning()
    {
        var plan = DmlGenerator.PlanInsert(new InsertRow([]), Target("Id"), DatabaseDialect.PostgreSql);
        Assert.Equal("INSERT INTO \"main\".\"T\" DEFAULT VALUES RETURNING \"Id\"", plan.Insert.Sql);
    }

    [Fact]
    public void NoGeneratedKey_PlainInsert()
    {
        var edit = new InsertRow([V("Name", "a")]);
        var plan = DmlGenerator.PlanInsert(edit, Target(), DatabaseDialect.PostgreSql);
        Assert.Null(plan.KeyQuery);
        Assert.Equal(DmlGenerator.Generate(edit, Target(), DatabaseDialect.PostgreSql).Sql, plan.Insert.Sql);
    }

    [Fact]
    public void TwoGeneratedKeys_Throw() =>
        Assert.Throws<NotSupportedException>(() =>
            DmlGenerator.PlanInsert(new InsertRow([V("Name", "a")]), Target("Id", "Id2"), DatabaseDialect.Sqlite));
}
