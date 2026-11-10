using Squill.Core;
using Squill.Core.Plans;
using Xunit;

namespace Squill.Data.SqlServer.Tests;

public sealed class SqlServerPlanParserTests
{
    private static string Sample(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Plans", name + ".xml"));

    private static QueryPlan Load(string name) => SqlServerPlanParser.Parse("select 1", Sample(name));

    private static string? Property(PlanNode node, string name) => node.Properties.FirstOrDefault(p => p.Name == name)?.Value;

    [Fact]
    public void SingleTable_IsOneClusteredScanWithItsPredicateAndCosts()
    {
        var plan = Load("single");
        Assert.Equal(DatabaseDialect.SqlServer, plan.Dialect);
        Assert.Equal(PlanMode.Estimated, plan.Mode);
        Assert.Equal(PlanSourceFormat.Xml, plan.SourceFormat);
        Assert.StartsWith("<ShowPlanXML", plan.SourceText, StringComparison.Ordinal);
        Assert.NotNull(plan.PlanningTimeMs);
        Assert.Null(plan.ExecutionTimeMs);
        var scan = plan.Root;
        Assert.Empty(scan.Children);
        Assert.Equal("Clustered Index Scan", scan.Operation);
        Assert.Equal(PlanNodeKind.TableScan, scan.Kind);
        Assert.StartsWith("[sample].[customers] · [PK__customer", scan.Target, StringComparison.Ordinal);
        Assert.Contains("name", scan.Detail, StringComparison.Ordinal);
        Assert.NotNull(scan.EstimatedRows);
        Assert.NotNull(scan.EstimatedCost);
        Assert.Null(scan.StartupCost);
        Assert.NotNull(scan.RowWidth);
        Assert.NotNull(Property(scan, "EstimateIO"));
        Assert.NotNull(Property(scan, "TableCardinality"));
        Assert.Equal("0", Property(scan, "Ordered"));
    }

    [Fact]
    public void JoinAggregateSort_NestThroughWrapperElements_AndMapKinds()
    {
        var plan = Load("join");
        var sort = plan.Root;
        Assert.Equal(PlanNodeKind.Sort, sort.Kind);
        Assert.Equal("Expr1002", sort.Detail);
        var aggregate = Assert.Single(sort.Children);
        Assert.Equal(PlanNodeKind.Aggregate, aggregate.Kind);
        var join = Assert.Single(aggregate.Children);
        Assert.Equal("Hash Match", join.Operation);
        Assert.Equal(PlanNodeKind.Join, join.Kind);
        Assert.Equal(2, join.Children.Count);
        Assert.All(join.Children, c => Assert.Equal(PlanNodeKind.TableScan, c.Kind));
        Assert.Contains(join.Children, c => c.Target!.StartsWith("[sample].[customers] [c] · ", StringComparison.Ordinal));
        Assert.Equal(Enumerable.Range(0, 5), plan.Flatten().Select(n => n.Id));
        Assert.True(sort.EstimatedCost >= aggregate.EstimatedCost);
    }

    [Fact]
    public void Top_IsALimit()
    {
        var plan = Load("top");
        Assert.Equal(PlanNodeKind.Limit, plan.Root.Kind);
        Assert.Single(plan.Root.Children);
    }

    [Fact]
    public void SeekWithKeyLookup_BothAreSeeksUnderNestedLoops()
    {
        var plan = Load("seek-lookup");
        Assert.Equal(PlanNodeKind.Join, plan.Root.Kind);
        Assert.Equal(["Index Seek", "Clustered Index Seek"], plan.Root.Children.Select(c => c.Operation));
        Assert.All(plan.Root.Children, c => Assert.Equal(PlanNodeKind.IndexSeek, c.Kind));
        Assert.Contains("orders_customer_placed", plan.Root.Children[0].Target, StringComparison.Ordinal);
        Assert.Equal("(7)", plan.Root.Children[0].Detail);
        Assert.Equal("1009", Property(plan.Root.Children[1], "EstimateRebinds"));
    }

    [Fact]
    public void MissingIndex_BecomesASuggestionAndAPlanWarning()
    {
        var plan = Load("missing-index");
        var suggestion = Assert.Single(plan.Suggestions);
        Assert.Equal("Missing index", suggestion.Title);
        Assert.Equal("CREATE INDEX [IX_orders_total_placed_on] ON [sample].[orders] ([total], [placed_on]);", suggestion.Text);
        var warning = Assert.Single(plan.Warnings, w => w.Code == PlanMessageCodes.MissingIndex);
        Assert.StartsWith("99.", Assert.Single(warning.Args), StringComparison.Ordinal);
    }

    [Fact]
    public void MissingIndex_WithIncludeColumns_AndLongNames_IsQuotedAndLimitedTo128()
    {
        var longColumn = new string('c', 150);
        var xml = Sample("missing-index").Replace("[placed_on]", "[" + longColumn + "]", StringComparison.Ordinal)
            .Replace("</ColumnGroup>\n", "</ColumnGroup>", StringComparison.Ordinal);
        var text = SqlServerPlanParser.Parse("select 1", xml).Suggestions[0].Text;
        Assert.Equal(128, text["CREATE INDEX [".Length..].IndexOf(']', StringComparison.Ordinal));

        var withInclude = Sample("missing-index").Replace(
            "</MissingIndex>",
            "<ColumnGroup Usage=\"INCLUDE\"><Column Name=\"[id]\" ColumnId=\"1\" /></ColumnGroup></MissingIndex>", StringComparison.Ordinal);
        Assert.EndsWith("([total], [placed_on]) INCLUDE ([id]);", SqlServerPlanParser.Parse("select 1", withInclude).Suggestions[0].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void ImplicitConversion_IsAPlanWarningWithTheExpression()
    {
        var plan = Load("sort-convert");
        Assert.Contains(plan.Warnings, w => w.Code == PlanMessageCodes.ImplicitConversion && w.Args[0].StartsWith("CONVERT_IMPLICIT(int,", StringComparison.Ordinal));
        Assert.Equal(PlanNodeKind.Sort, plan.Root.Kind);
    }

    [Fact]
    public void NodeWarnings_AreMappedByOperator()
    {
        var spill = Sample("sort-convert").Replace(
            "<RelOp NodeId=\"0\" PhysicalOp=\"Sort\"", "<RelOp NodeId=\"0\" PhysicalOp=\"Sort\" X=\"1\"", StringComparison.Ordinal);
        var withWarnings = spill.Replace(
            "<OutputList>",
            "<Warnings NoJoinPredicate=\"true\"><SpillToTempDb SpillLevel=\"1\" /><ColumnsWithNoStatistics><ColumnReference Column=\"name\" /></ColumnsWithNoStatistics></Warnings><OutputList>",
            StringComparison.Ordinal);
        var warnings = SqlServerPlanParser.Parse("select 1", withWarnings).Root.Warnings.Select(w => w.Code).ToList();
        Assert.Contains(PlanMessageCodes.SortSpill, warnings);
        Assert.Contains(PlanMessageCodes.NoStats, warnings);
        Assert.Contains(PlanMessageCodes.NoJoinPredicate, warnings);
    }

    [Fact]
    public void SeveralStatements_OnlyTheFirstIsReadAndTheOthersAreCounted()
    {
        var xml = Sample("single");
        var statement = xml[xml.IndexOf("<StmtSimple", StringComparison.Ordinal)..(xml.IndexOf("</StmtSimple>", StringComparison.Ordinal) + "</StmtSimple>".Length)];
        var plan = SqlServerPlanParser.Parse("select 1", xml.Replace(statement, statement + statement, StringComparison.Ordinal));
        Assert.Equal("1", Property(plan.Root, "Other statements"));
    }

    [Theory]
    [InlineData("this is not xml")]
    [InlineData("<other/>")]
    [InlineData("<ShowPlanXML xmlns=\"http://schemas.microsoft.com/sqlserver/2004/07/showplan\"/>")]
    public void Garbage_RaisesPlanParseExceptionKeepingTheText(string text)
    {
        var ex = Assert.Throws<PlanParseException>(() => SqlServerPlanParser.Parse("select 1", text));
        Assert.Equal(text, ex.SourceText);
    }
}
