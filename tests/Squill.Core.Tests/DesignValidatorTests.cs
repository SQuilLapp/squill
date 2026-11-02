using Squill.Core.Design;
using Xunit;

namespace Squill.Core.Tests;

public sealed class DesignValidatorTests
{
    private static ColumnDesign Col(string name, bool nullable = false, bool auto = false, string type = "int") => new(null, name, type, nullable, null, auto);

    private static TableDesign Clean() => new(
        "public", "t", [Col("id"), Col("a", true)], ["id"], null,
        [new IndexDesign(null, "ix", false, ["a"])],
        [new ForeignKeyDesign(null, "fk", ["a"], "public", "p", ["id"], ReferentialAction.NoAction, ReferentialAction.NoAction)]);

    private static TableDesign Existing() => TableDesign.FromTable(new TableInfo(
        "public", "t", TableKind.Table,
        [new ColumnDefinition("id", "int", false, true, null), new ColumnDefinition("a", "int", true, false, null)],
        [], [], "pk"));

    private static string[] Messages(TableDesign d, TableDesign? o = null, DatabaseDialect dialect = DatabaseDialect.PostgreSql) =>
        [.. DesignValidator.Validate(d, o, dialect).Select(i => $"{i.Severity}:{i.Subject}")];

    [Fact]
    public void CleanNewTable_HasNoIssue() => Assert.Empty(DesignValidator.Validate(Clean(), null, DatabaseDialect.PostgreSql));

    [Fact]
    public void CleanEdit_HasNoIssue() => Assert.Empty(DesignValidator.Validate(Existing(), Existing(), DatabaseDialect.PostgreSql));

    [Fact]
    public void EmptyTableName() => Assert.Equal(["Error:"], Messages(Clean() with { Name = " " }));

    [Fact]
    public void NoColumns() => Assert.Contains("Error:", Messages(TableDesign.New(null, "t")));

    [Fact]
    public void EmptyAndDuplicateColumnNames()
    {
        var d = Clean() with { Columns = [Col("id"), Col("ID"), Col("")], PrimaryKey = [], Indexes = [], ForeignKeys = [] };
        Assert.Equal(["Error:ID", "Error:"], Messages(d));
    }

    [Fact]
    public void EmptyDataType() => Assert.Equal(["Error:id"], Messages(Clean() with { Columns = [Col("id", type: ""), Col("a", true)] }));

    [Fact]
    public void PrimaryKey_MissingTwiceAndNullable()
    {
        Assert.Equal(["Error:zz"], Messages(Clean() with { PrimaryKey = ["zz"] }));
        Assert.Equal(["Error:id"], Messages(Clean() with { PrimaryKey = ["id", "id"] }));
        Assert.Equal(["Error:a"], Messages(Clean() with { PrimaryKey = ["a"] }));
    }

    [Fact]
    public void MoreThanOneAutoIncrement() =>
        Assert.Contains("Error:", Messages(Clean() with { Columns = [Col("id", auto: true), Col("a", auto: true)] }));

    [Fact]
    public void AutoIncrement_SqliteNeedsSolePrimaryKey()
    {
        var d = Clean() with { Schema = null, Columns = [Col("id", auto: true), Col("b")], PrimaryKey = ["id", "b"], Indexes = [], ForeignKeys = [] };
        Assert.Equal(["Error:id"], Messages(d, null, DatabaseDialect.Sqlite));
        Assert.Empty(Messages(d with { PrimaryKey = ["id"] }, null, DatabaseDialect.Sqlite));
    }

    [Fact]
    public void AutoIncrement_MySqlNeedsPrimaryKeyMembership()
    {
        var d = Clean() with { Columns = [Col("id"), Col("b", auto: true)], Indexes = [], ForeignKeys = [] };
        Assert.Equal(["Error:b"], Messages(d, null, DatabaseDialect.MySql));
        Assert.Empty(Messages(d, null, DatabaseDialect.PostgreSql));
    }

    [Fact]
    public void AutoIncrement_ToggledOnExistingColumn()
    {
        var o = Existing();
        var e = o with { Columns = [.. o.Columns.Select(c => c.Name == "a" ? c with { IsAutoIncrement = true } : c)] };
        Assert.Equal(["Error:a"], Messages(e, o));
    }

    [Fact]
    public void Indexes_EmptyDuplicateMissingColumnAndNoColumns()
    {
        var ix = new IndexDesign(null, "ix", false, ["a"]);
        Assert.Equal(["Error:"], Messages(Clean() with { Indexes = [ix with { Name = "" }] }));
        Assert.Equal(["Error:IX"], Messages(Clean() with { Indexes = [ix, ix with { Name = "IX" }] }));
        Assert.Equal(["Error:ix"], Messages(Clean() with { Indexes = [ix with { Columns = ["zz"] }] }));
        Assert.Equal(["Error:ix"], Messages(Clean() with { Indexes = [ix with { Columns = [] }] }));
    }

    [Fact]
    public void ForeignKeys_Rules()
    {
        var fk = Clean().ForeignKeys[0];
        Assert.Equal(["Error:"], Messages(Clean() with { ForeignKeys = [fk with { Name = "" }] }));
        Assert.Equal(["Error:FK"], Messages(Clean() with { ForeignKeys = [fk, fk with { Name = "FK" }] }));
        Assert.Equal(["Error:fk"], Messages(Clean() with { ForeignKeys = [fk with { Columns = ["zz"] }] }));
        Assert.Equal(["Error:fk", "Error:fk"], Messages(Clean() with { ForeignKeys = [fk with { Columns = [] }] }));
        Assert.Equal(["Error:fk"], Messages(Clean() with { ForeignKeys = [fk with { ReferencedColumns = ["id", "x"] }] }));
        Assert.Equal(["Error:fk"], Messages(Clean() with { ForeignKeys = [fk with { ReferencedTable = " " }] }));
    }

    [Fact]
    public void SchemaChange_IsError() => Assert.Equal(["Error:"], Messages(Existing() with { Schema = "other" }, Existing()));

    [Fact]
    public void Sqlite_OnlyMainSchema()
    {
        Assert.Equal(["Error:"], Messages(Clean() with { Schema = "x" }, null, DatabaseDialect.Sqlite));
        Assert.Empty(Messages(Clean() with { Schema = "main" }, null, DatabaseDialect.Sqlite));
        Assert.Empty(Messages(Clean() with { Schema = null }, null, DatabaseDialect.Sqlite));
    }

    [Fact]
    public void Warning_DropColumn()
    {
        var o = Existing();
        Assert.Equal(["Warning:a"], Messages(o with { Columns = [o.Columns[0]] }, o));
    }

    [Fact]
    public void Warning_TypeChange()
    {
        var o = Existing();
        Assert.Equal(["Warning:a"], Messages(o with { Columns = [o.Columns[0], o.Columns[1] with { DataType = "text" }] }, o));
    }

    [Fact]
    public void Warning_NullableToNotNull_ButNotTheReverse()
    {
        var o = Existing();
        var tightened = o with { Columns = [o.Columns[0], o.Columns[1] with { IsNullable = false }] };
        Assert.Equal(["Warning:a"], Messages(tightened, o));
        Assert.Empty(Messages(o, tightened));
    }
}
