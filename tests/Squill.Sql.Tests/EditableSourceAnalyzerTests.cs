using Squill.Core;
using Squill.Sql.Editing;
using Xunit;

namespace Squill.Sql.Tests;

public sealed class EditableSourceAnalyzerTests
{
    private static ColumnDefinition Col(string name, bool pk = false) => new(name, "int", !pk, pk, null);

    private static TableInfo Table(string schema, string name, TableKind kind, params ColumnDefinition[] cols) =>
        new(schema, name, kind, cols, [], []);

    private static readonly SchemaSnapshot Schema = new("main",
    [
        Table("main", "Users", TableKind.Table, Col("Id", true), Col("Name"), Col("Odd Name")),
        Table("main", "Pairs", TableKind.Table, Col("A", true), Col("B", true), Col("C")),
        Table("main", "NoKey", TableKind.Table, Col("X")),
        Table("main", "V", TableKind.View, Col("Id", true)),
    ]);

    private static IReadOnlyList<ColumnInfo> Cols(params string[] names) =>
        [.. names.Select((n, i) => new ColumnInfo(n, "int", typeof(int), true, i))];

    private static EditableSourceResult Run(string sql, DatabaseDialect d, params string[] cols) =>
        EditableSourceAnalyzer.Analyze(sql, d, Schema, Cols(cols));

    public static TheoryData<DatabaseDialect> Dialects => new()
    {
        DatabaseDialect.Sqlite, DatabaseDialect.PostgreSql, DatabaseDialect.MySql, DatabaseDialect.SqlServer,
    };

    [Theory, MemberData(nameof(Dialects))]
    public void SelectStar_MapsEveryColumn(DatabaseDialect d)
    {
        var r = Run("SELECT * FROM Users", d, "Id", "Name", "Odd Name");
        Assert.Null(r.Reason);
        Assert.Equal(3, r.Target!.Columns.Count);
        Assert.Equal(["Id"], r.Target.KeyColumns);
        Assert.Equal("Users", r.Target.Table);
    }

    [Theory, MemberData(nameof(Dialects))]
    public void ExplicitColumnsWhereOrderLimit_AreEditable(DatabaseDialect d)
    {
        var sql = d == DatabaseDialect.SqlServer
            ? "SELECT TOP 5 u.id, u.name FROM main.Users u WHERE u.id > 1 ORDER BY u.id"
            : "SELECT u.id, u.name FROM main.Users AS u WHERE u.id > 1 ORDER BY u.id LIMIT 5 OFFSET 1";
        var r = Run(sql, d, "id", "name");
        Assert.Null(r.Reason);
        Assert.Equal("Id", r.Target!.Columns[0].Definition.Name);
        Assert.Equal("main", r.Target.Schema);
    }

    [Theory]
    [InlineData(DatabaseDialect.Sqlite, "SELECT \"Id\", \"Odd Name\" FROM \"Users\"")]
    [InlineData(DatabaseDialect.PostgreSql, "SELECT \"Id\", \"Odd Name\" FROM \"main\".\"Users\"")]
    [InlineData(DatabaseDialect.MySql, "SELECT `Id`, `Odd Name` FROM `Users`")]
    [InlineData(DatabaseDialect.SqlServer, "SELECT [Id], [Odd Name] FROM [main].[Users]")]
    public void QuotedNames_AreEditable(DatabaseDialect d, string sql)
    {
        var r = Run(sql, d, "Id", "Odd Name");
        Assert.Null(r.Reason);
        Assert.Equal(2, r.Target!.Columns.Count);
    }

    [Fact]
    public void QualifiedStar_WithAlias_IsEditable()
    {
        var r = Run("SELECT u.* FROM Users u", DatabaseDialect.Sqlite, "Id", "Name", "Odd Name");
        Assert.Equal(3, r.Target!.Columns.Count);
    }

    [Fact]
    public void ComputedColumn_IsReadOnlyRestEditable()
    {
        var r = Run("SELECT Id, Name, Id + 1 AS Next FROM Users", DatabaseDialect.Sqlite, "Id", "Name", "Next");
        Assert.Equal(2, r.Target!.Columns.Count);
        Assert.False(r.Target.IsEditable(2));
    }

    [Fact]
    public void RenamingAlias_IsReadOnly()
    {
        var r = Run("SELECT Id, Name AS Label FROM Users", DatabaseDialect.Sqlite, "Id", "Label");
        Assert.Single(r.Target!.Columns);
        Assert.False(r.Target.IsEditable(1));
    }

    [Fact]
    public void DefinitionUsesTableSpelling()
    {
        var r = Run("SELECT id FROM users", DatabaseDialect.Sqlite, "id");
        Assert.Equal("Id", r.Target!.Columns[0].Definition.Name);
    }

    [Theory]
    [InlineData("SELECT * FROM Users; SELECT * FROM Users", "not a single statement")]
    [InlineData("SELECT * FROM", "not a single statement")]
    [InlineData("DELETE FROM Users", "not a plain SELECT")]
    [InlineData("SELECT Id FROM Users UNION SELECT Id FROM Users", "not a plain SELECT")]
    [InlineData("WITH c AS (SELECT 1) SELECT * FROM Users", "query is aggregated or uses WITH/DISTINCT")]
    [InlineData("SELECT DISTINCT Id FROM Users", "query is aggregated or uses WITH/DISTINCT")]
    [InlineData("SELECT Id FROM Users GROUP BY Id", "query is aggregated or uses WITH/DISTINCT")]
    [InlineData("SELECT Id FROM Users GROUP BY Id HAVING Id > 1", "query is aggregated or uses WITH/DISTINCT")]
    [InlineData("SELECT 1", "query does not read a single table")]
    [InlineData("SELECT * FROM Users a JOIN Pairs b ON a.Id = b.A", "query does not read a single table")]
    [InlineData("SELECT * FROM (SELECT * FROM Users) s", "query does not read a single table")]
    [InlineData("SELECT * FROM Missing", "source is not a table")]
    [InlineData("SELECT * FROM V", "source is not a table")]
    [InlineData("SELECT * FROM NoKey", "table has no primary key")]
    public void ReadOnlyReasons(string sql, string reason)
    {
        var r = Run(sql, DatabaseDialect.Sqlite, "Id", "Name");
        Assert.Null(r.Target);
        Assert.Equal(reason, r.Reason);
    }

    [Fact]
    public void DuplicateColumnNames_AreReadOnly()
    {
        var r = Run("SELECT Id, Id FROM Users", DatabaseDialect.Sqlite, "Id", "id");
        Assert.Equal("duplicate column names", r.Reason);
    }

    [Fact]
    public void MissingKeyColumn_IsReadOnly()
    {
        var r = Run("SELECT Name FROM Users", DatabaseDialect.Sqlite, "Name");
        Assert.Equal("primary key column Id is not selected", r.Reason);
        var composite = Run("SELECT A, C FROM Pairs", DatabaseDialect.Sqlite, "A", "C");
        Assert.Equal("primary key column B is not selected", composite.Reason);
    }
}
