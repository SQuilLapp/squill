using Squill.Core;
using Squill.Core.Plans;
using Squill.Presentation.Plans;
using Xunit;

namespace Squill.Presentation.Tests.Plans;

public sealed class PlanLayoutTests
{
    private static PlanNode Node(string op, double? rows = null, bool helper = false, IReadOnlyList<PlanWarning>? warnings = null, params PlanNode[] children) =>
        new(0, op, helper ? PlanNodeKind.Other : PlanNodeKind.TableScan, null, null, rows, rows is null ? null : 1, null, null, null, null, null, [], warnings ?? [], helper, children);

    private static IReadOnlyList<PlanRowViewModel> Rows(PlanNode root, DatabaseDialect dialect = DatabaseDialect.PostgreSql) =>
        PlanViewModel.Create(new QueryPlan(dialect, PlanMode.Estimated, "select 1", PlanIds.Assign(root), [], [], null, null, null, false, PlanSourceFormat.Json, "{}")).Rows;

    private static double Width(string s) => s.Length * 7;

    // Aggregate > Sort > Limit > Hash Join > (Scan, Hash > Hash Join > (Scan, Hash > Scan)): 8 cards, 2 pills.
    private static PlanNode Mockup() =>
        Node("Aggregate", 1, children: Node("Sort", 1, children: Node("Limit", 5, children: Node("Hash Join", 100, children:
        [
            Node("Seq Scan", 1000),
            Node("Hash", 84_000, helper: true, children: Node("Hash Join", 500, children: [Node("Seq Scan", 20), Node("Hash", 212_000, helper: true, children: Node("Seq Scan", 7))])),
        ]))));

    [Fact]
    public void MockupShape_HasCardsPillsAndRootOnTheRight()
    {
        var rows = Rows(Mockup());
        var r = PlanLayout.Compute(rows, Width);
        Assert.Equal(8, r.Nodes.Count);
        Assert.Equal(2, r.Pills.Count);
        Assert.Equal(7, r.Edges.Count);
        Assert.Equal(r.Nodes.Max(b => b.X), r.Nodes.Single(b => b.Id == 0).X);
        Assert.Equal(24, r.Nodes.Min(b => b.X));
        Assert.Contains("Hash 84 k", r.Pills[0].Text);
        Assert.Contains("212 k", r.Pills[1].Text);
    }

    [Fact]
    public void PillLiesBetweenItsChildAndParentCards()
    {
        var r = PlanLayout.Compute(Rows(Mockup()), Width);
        var pill = r.Pills[0];
        var edge = r.Edges.Single(e => e.X0 < pill.X + pill.Width / 2 && e.X1 > pill.X + pill.Width / 2 && Math.Abs((e.Y0 + e.Y1) / 2 - (pill.Y + pill.Height / 2)) < 0.01);
        Assert.Equal((edge.X0 + edge.X1) / 2, pill.X + pill.Width / 2, 6);
    }

    [Fact]
    public void Pills_NeverOverlapACard()
    {
        var r = PlanLayout.Compute(Rows(Mockup()), s => s.Length * 9.0);
        foreach (var pill in r.Pills)
        {
            Assert.All(r.Nodes, b => Assert.True(
                pill.X + pill.Width <= b.X || b.X + b.Width <= pill.X || pill.Y + pill.Height <= b.Y || b.Y + b.Height <= pill.Y,
                $"Pill {pill.Id} overlaps card {b.Id}."));
        }
    }

    [Fact]
    public void InnerNode_IsCenteredOnItsFirstAndLastChild()
    {
        var root = Node("Join", 1, children: [Node("A", 1), Node("B", 1), Node("C", 1)]);
        var r = PlanLayout.Compute(Rows(root), Width);
        var j = r.Nodes[0];
        Assert.Equal((r.Nodes[1].Y + r.Nodes[3].Y) / 2, j.Y, 6);
        Assert.True(r.Nodes[1].Y < r.Nodes[2].Y && r.Nodes[2].Y < r.Nodes[3].Y);
    }

    [Fact]
    public void BushyTree_NoCardsOverlap_AndIsDeterministic()
    {
        var rng = new Random(42);
        var count = 0;
        PlanNode Build(int depth)
        {
            count++;
            var kids = new List<PlanNode>();
            while (count < 200 && depth < 12 && kids.Count < 4 && (depth == 0 || rng.Next(4) != 0))
            {
                kids.Add(Build(depth + 1));
            }

            var warn = rng.Next(4) == 0 ? new[] { new PlanWarning(PlanMessageCodes.FullScan, []), new PlanWarning(PlanMessageCodes.SortSpill, []) } : [];
            return Node("Op" + count, rng.Next(1, 100000), rng.Next(10) == 0 && kids.Count == 1, warn, [.. kids]);
        }

        var tree = Build(0);
        var rows = Rows(tree);
        var a = PlanLayout.Compute(rows, Width);
        var b = PlanLayout.Compute(rows, Width);
        Assert.Equal(a.Nodes, b.Nodes);
        Assert.Equal(a.Edges, b.Edges);
        Assert.Equal(a.Pills, b.Pills);
        Assert.True(a.Nodes.Count > 20);
        for (var i = 0; i < a.Nodes.Count; i++)
        {
            for (var j = i + 1; j < a.Nodes.Count; j++)
            {
                var p = a.Nodes[i];
                var q = a.Nodes[j];
                var apart = p.X + p.Width <= q.X || q.X + q.Width <= p.X || p.Y + p.Height <= q.Y || q.Y + q.Height <= p.Y;
                Assert.True(apart, $"{p.Id} overlaps {q.Id}");
            }
        }

        Assert.All(a.Nodes, p => Assert.True(p.X + p.Width <= a.Width && p.Y + p.Height <= a.Height && p.Y >= 24));
    }

    [Fact]
    public void Thickness_GrowsWithRows_WithinBounds()
    {
        var root = Node("Join", 1, children: [Node("A", 10), Node("B", 1000), Node("C", 1_000_000)]);
        var t = PlanLayout.Compute(Rows(root), Width).Edges.Select(e => e.Thickness).ToArray();
        Assert.True(t[0] < t[1] && t[1] < t[2]);
        Assert.All(t, v => Assert.InRange(v, 1.5, 7.5));
    }

    [Fact]
    public void SingleNode_HasOneCardAndNoEdge()
    {
        var r = PlanLayout.Compute(Rows(Node("Scan", 5)), Width);
        Assert.Single(r.Nodes);
        Assert.Empty(r.Edges);
        Assert.Equal(24, r.Nodes[0].X);
        Assert.Equal(24 + 250 + 24, r.Width);
        Assert.Equal(24 + 90 + 24, r.Height);
    }

    [Fact]
    public void SqliteShape_HasFlatThicknessAndNoExtraLines()
    {
        var r = PlanLayout.Compute(Rows(Node("SCAN", null, children: [Node("SEARCH"), Node("SCAN")]), DatabaseDialect.Sqlite), Width);
        Assert.All(r.Edges, e => Assert.Equal(1.5, e.Thickness));
        Assert.All(r.Nodes, b => Assert.Equal(0, b.ExtraLines));
        Assert.All(r.Nodes, b => Assert.Equal(90, b.Height));
    }

    [Fact]
    public void WarningsAndSpill_GrowTheCard_UpToTwoLines()
    {
        var node = Node("Sort", 1, warnings: [new PlanWarning(PlanMessageCodes.FullScan, []), new PlanWarning(PlanMessageCodes.SortSpill, [])]);
        var box = PlanLayout.Compute(Rows(node), Width).Nodes[0];
        Assert.Equal(2, box.ExtraLines);
        Assert.Equal(130, box.Height);
    }
}
