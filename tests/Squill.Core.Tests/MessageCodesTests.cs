using System.Text.RegularExpressions;
using Squill.Core.Design;
using Xunit;

namespace Squill.Core.Tests;

public sealed partial class MessageCodesTests
{
    [GeneratedRegex("public const string \\w+ = \"([^\"]+)\"")]
    private static partial Regex Constant();

    private static ColumnDesign Col(string name, bool nullable = false, bool auto = false, string type = "int") => new(null, name, type, nullable, null, auto);

    private static ForeignKeyDesign Fk(string name = "fk", string[]? columns = null, string table = "p", string[]? referenced = null) =>
        new(null, name, columns ?? ["a"], "public", table, referenced ?? ["id"], ReferentialAction.NoAction, ReferentialAction.NoAction);

    private static TableDesign Clean() => new(
        "public", "t", [Col("id"), Col("a", true)], ["id"], null,
        [new IndexDesign(null, "ix", false, ["a"])],
        [Fk()]);

    private static TableDesign Existing() => TableDesign.FromTable(new TableInfo(
        "public", "t", TableKind.Table,
        [new ColumnDefinition("id", "int", false, true, null), new ColumnDefinition("a", "int", true, false, null)],
        [], [], "pk"));

    private static readonly TableDesign Plain = Clean() with { Indexes = [], ForeignKeys = [] };

    public static TheoryData<TableDesign, TableDesign?, DatabaseDialect, string, string> Cases()
    {
        var sqlite = Plain with { Schema = null, Columns = [Col("id", auto: true), Col("b")], PrimaryKey = ["id", "b"] };
        var mySql = Plain with { Columns = [Col("id", auto: true), Col("b")], PrimaryKey = ["b"] };
        var changed = Existing() with { Columns = [Existing().Columns[0] with { IsAutoIncrement = true }, Existing().Columns[1]] };
        var typeChanged = Existing() with { Columns = [Existing().Columns[0], Existing().Columns[1] with { DataType = "text" }] };
        var notNull = Existing() with { Columns = [Existing().Columns[0], Existing().Columns[1] with { IsNullable = false }] };
        var dropped = Existing() with { Columns = [Existing().Columns[0]] };
        return new()
        {
            { Clean() with { Name = " " }, null, DatabaseDialect.PostgreSql, CoreMessageCodes.DesignTableNameEmpty, "" },
            { TableDesign.New(null, "t"), null, DatabaseDialect.PostgreSql, CoreMessageCodes.DesignNoColumn, "" },
            { Plain with { Columns = [Col("")], PrimaryKey = [] }, null, DatabaseDialect.PostgreSql, CoreMessageCodes.DesignColumnNameEmpty, "" },
            { Plain with { Columns = [Col("id"), Col("ID")], PrimaryKey = [] }, null, DatabaseDialect.PostgreSql, CoreMessageCodes.DesignColumnNameDuplicate, "ID" },
            { Plain with { Columns = [Col("id", type: "")], PrimaryKey = [] }, null, DatabaseDialect.PostgreSql, CoreMessageCodes.DesignColumnTypeMissing, "id" },
            { Plain with { PrimaryKey = ["zz"] }, null, DatabaseDialect.PostgreSql, CoreMessageCodes.DesignPkColumnMissing, "zz" },
            { Plain with { PrimaryKey = ["id", "id"] }, null, DatabaseDialect.PostgreSql, CoreMessageCodes.DesignPkColumnDuplicate, "id" },
            { Plain with { PrimaryKey = ["a"] }, null, DatabaseDialect.PostgreSql, CoreMessageCodes.DesignPkColumnNullable, "a" },
            { Plain with { Columns = [Col("id", auto: true), Col("a", auto: true)] }, null, DatabaseDialect.PostgreSql, CoreMessageCodes.DesignAutoIncrementMultiple, "" },
            { sqlite, null, DatabaseDialect.Sqlite, CoreMessageCodes.DesignAutoIncrementNotOnlyPk, "id" },
            { mySql, null, DatabaseDialect.MySql, CoreMessageCodes.DesignAutoIncrementNotInPk, "id" },
            { changed, Existing(), DatabaseDialect.PostgreSql, CoreMessageCodes.DesignAutoIncrementChange, "id" },
            { Plain with { Indexes = [new IndexDesign(null, "", false, ["a"])] }, null, DatabaseDialect.PostgreSql, CoreMessageCodes.DesignIndexNameEmpty, "" },
            { Plain with { Indexes = [new IndexDesign(null, "ix", false, ["a"]), new IndexDesign(null, "IX", false, ["a"])] }, null, DatabaseDialect.PostgreSql, CoreMessageCodes.DesignIndexNameDuplicate, "IX" },
            { Plain with { Indexes = [new IndexDesign(null, "ix", false, [])] }, null, DatabaseDialect.PostgreSql, CoreMessageCodes.DesignIndexNoColumn, "ix" },
            { Plain with { Indexes = [new IndexDesign(null, "ix", false, ["zz"])] }, null, DatabaseDialect.PostgreSql, CoreMessageCodes.DesignIndexColumnMissing, "ix,zz" },
            { Plain with { ForeignKeys = [Fk("")] }, null, DatabaseDialect.PostgreSql, CoreMessageCodes.DesignForeignKeyNameEmpty, "" },
            { Plain with { ForeignKeys = [Fk(), Fk("FK")] }, null, DatabaseDialect.PostgreSql, CoreMessageCodes.DesignForeignKeyNameDuplicate, "FK" },
            { Plain with { ForeignKeys = [Fk(columns: [], referenced: [])] }, null, DatabaseDialect.PostgreSql, CoreMessageCodes.DesignForeignKeyNoColumn, "fk" },
            { Plain with { ForeignKeys = [Fk(columns: ["zz"])] }, null, DatabaseDialect.PostgreSql, CoreMessageCodes.DesignForeignKeyColumnMissing, "fk,zz" },
            { Plain with { ForeignKeys = [Fk(referenced: ["id", "x"])] }, null, DatabaseDialect.PostgreSql, CoreMessageCodes.DesignForeignKeyColumnCount, "fk,1,2" },
            { Plain with { ForeignKeys = [Fk(table: "")] }, null, DatabaseDialect.PostgreSql, CoreMessageCodes.DesignForeignKeyNoTable, "fk" },
            { Existing() with { Schema = "other" }, Existing(), DatabaseDialect.PostgreSql, CoreMessageCodes.DesignSchemaMove, "" },
            { Plain with { Schema = "other" }, null, DatabaseDialect.Sqlite, CoreMessageCodes.DesignSqliteSchema, "other" },
            { dropped, Existing(), DatabaseDialect.PostgreSql, CoreMessageCodes.DesignDropColumnDataLoss, "a" },
            { typeChanged, Existing(), DatabaseDialect.PostgreSql, CoreMessageCodes.DesignTypeChangeRisk, "a" },
            { notNull, Existing(), DatabaseDialect.PostgreSql, CoreMessageCodes.DesignNullsBlockNotNull, "a" },
        };
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Validate_ReturnsTheCodeAndArgsOfEachIssue(TableDesign edited, TableDesign? original, DatabaseDialect dialect, string code, string args)
    {
        var issues = DesignValidator.Validate(edited, original, dialect);
        Assert.Contains(issues, i => i.Code == code && string.Join(",", i.Args ?? []) == args);
        Assert.All(issues, i => Assert.NotNull(i.Code));
    }

    [Fact]
    public void All_ListsEveryConstantOfTheSource()
    {
        var root = AppContext.BaseDirectory;
        while (root is not null && !Directory.Exists(Path.Combine(root, "src")))
            root = Path.GetDirectoryName(root);
        Assert.NotNull(root);

        var source = File.ReadAllText(Path.Combine(root, "src", "Squill.Core", "CoreMessageCodes.cs"));
        Assert.Equal(Constant().Matches(source).Select(m => m.Groups[1].Value).Order(), CoreMessageCodes.All.Order());
    }

    [Fact]
    public void EveryDesignCodeIsRaisedByTheValidatorExceptTheTwoOfTheDesignerViewModel()
    {
        var raised = Cases().Select(c => c.Data.Item4).ToHashSet();
        var expected = CoreMessageCodes.All.Where(c => c.StartsWith("design.", StringComparison.Ordinal)).ToHashSet();
        expected.ExceptWith([CoreMessageCodes.DesignForeignKeyTableMissing, CoreMessageCodes.DesignViewsNotRecreated]);
        Assert.Equal(expected.Order(), raised.Order());
    }
}
