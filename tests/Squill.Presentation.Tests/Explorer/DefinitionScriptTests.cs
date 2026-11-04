using Squill.Core;
using Squill.Presentation.Explorer;
using Xunit;

namespace Squill.Presentation.Tests.Explorer;

public sealed class DefinitionScriptTests
{
    private static TableInfo View(string? schema, string? definition, TableKind kind = TableKind.View) =>
        new(schema, "v", kind, [], [], [], null, definition);

    [Theory]
    [InlineData(DatabaseDialect.PostgreSql, "CREATE VIEW \"s\".\"v\" AS\nSELECT 1;")]
    [InlineData(DatabaseDialect.MySql, "CREATE VIEW `s`.`v` AS\nSELECT 1;")]
    public void View_BodyOnlyDialects_AreWrappedInCreateView(DatabaseDialect dialect, string expected) =>
        Assert.Equal(expected, DefinitionScript.View(dialect, View("s", " SELECT 1; ")));

    [Theory]
    [InlineData(DatabaseDialect.SqlServer)]
    [InlineData(DatabaseDialect.Sqlite)]
    public void View_FullStatementDialects_AreUsedAsStored(DatabaseDialect dialect) =>
        Assert.Equal("CREATE VIEW v AS SELECT 1", DefinitionScript.View(dialect, View(null, "CREATE VIEW v AS SELECT 1")));

    [Fact]
    public void View_MaterializedOnPostgres_UsesMaterializedKeyword() =>
        Assert.StartsWith("CREATE MATERIALIZED VIEW \"s\".\"v\" AS", DefinitionScript.View(DatabaseDialect.PostgreSql, View("s", "SELECT 1", TableKind.MaterializedView)));

    [Theory]
    [InlineData(DatabaseDialect.PostgreSql)]
    [InlineData(DatabaseDialect.MySql)]
    [InlineData(DatabaseDialect.SqlServer)]
    [InlineData(DatabaseDialect.Sqlite)]
    public void AnyObject_WithoutDefinition_ReturnsNull(DatabaseDialect dialect)
    {
        Assert.Null(DefinitionScript.View(dialect, View("s", null)));
        Assert.Null(DefinitionScript.Routine(dialect, new RoutineInfo("s", "f", RoutineKind.Function, "f()", "int", null)));
        Assert.Null(DefinitionScript.Trigger(dialect, new TriggerInfo("s", "t", "tr", "AFTER", "INSERT", null)));
    }

    [Theory]
    [InlineData(DatabaseDialect.PostgreSql)]
    [InlineData(DatabaseDialect.SqlServer)]
    [InlineData(DatabaseDialect.Sqlite)]
    public void RoutineAndTrigger_FullStatementDialects_AreUsedAsStored(DatabaseDialect dialect)
    {
        Assert.Equal("CREATE FUNCTION f() ...", DefinitionScript.Routine(dialect, new RoutineInfo("s", "f", RoutineKind.Function, "f()", "int", "CREATE FUNCTION f() ...")));
        Assert.Equal("CREATE TRIGGER tr ...", DefinitionScript.Trigger(dialect, new TriggerInfo("s", "t", "tr", "AFTER", "INSERT", "CREATE TRIGGER tr ...")));
    }

    [Fact]
    public void Routine_MySql_BuildsHeaderFromSignature()
    {
        Assert.Equal(
            "CREATE FUNCTION add_two(a int, b int)\nRETURNS int\nRETURN a + b",
            DefinitionScript.Routine(DatabaseDialect.MySql, new RoutineInfo("d", "add_two", RoutineKind.Function, "add_two(a int, b int)", "int", "RETURN a + b")));
        Assert.Equal(
            "CREATE PROCEDURE bump(n int)\nSET @x = n",
            DefinitionScript.Routine(DatabaseDialect.MySql, new RoutineInfo("d", "bump", RoutineKind.Procedure, "bump(n int)", null, "SET @x = n")));
    }

    [Fact]
    public void Trigger_MySql_BuildsHeaderFromTimingAndEvent() =>
        Assert.Equal(
            "CREATE TRIGGER `tr` BEFORE INSERT\nON `person` FOR EACH ROW\nSET new.a = 1",
            DefinitionScript.Trigger(DatabaseDialect.MySql, new TriggerInfo("d", "person", "tr", "BEFORE", "INSERT", "SET new.a = 1")));

    [Theory]
    [InlineData(DatabaseDialect.PostgreSql)]
    [InlineData(DatabaseDialect.MySql)]
    [InlineData(DatabaseDialect.SqlServer)]
    [InlineData(DatabaseDialect.Sqlite)]
    public void CreateTable_AnyDialect_ContainsTableAndColumns(DatabaseDialect dialect)
    {
        var table = new TableInfo(dialect is DatabaseDialect.Sqlite or DatabaseDialect.MySql ? null : "s", "person",
            TableKind.Table, [new ColumnDefinition("id", "int", false, true, null)], [], []);
        var script = DefinitionScript.CreateTable(dialect, table);
        Assert.Contains("CREATE TABLE", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("person", script);
        Assert.Contains("id", script);
        Assert.EndsWith(";", script);
    }
}
