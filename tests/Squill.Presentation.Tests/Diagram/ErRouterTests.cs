using Squill.Core;
using Squill.Presentation.Diagram;
using Xunit;

namespace Squill.Presentation.Tests.Diagram;

public sealed class ErRouterTests
{
    private static readonly ErLayoutOptions Opts = new(s => s.Length * 7, HeaderHeight: 28, RowHeight: 20, MinWidth: 160);

    private static ErEntity E(string name) =>
        new(name, null, name, TableKind.Table, [.. Enumerable.Range(0, 3).Select(i => new ErColumn("c" + i, "int", i == 0, false, false))]);

    private static ErRelation R(string from, string to, string[]? fromCols = null, string[]? toCols = null) =>
        new("fk_" + from + "_" + to, from, fromCols ?? ["c1"], to, toCols ?? ["c0"], ErCardinality.ManyToOne, false);

    private static ErModel M(string[] names, params ErRelation[] rels) => new([.. names.Select(E)], rels, 0);

    private static ErBox B(double x, double y) => new(x, y, 160, 88);

    [Fact]
    public void FacingBoxes_FourPointsAtTheRightRows()
    {
        var model = M(["P", "C"], R("C", "P"));
        var edge = Assert.Single(ErRouter.Route(model, new Dictionary<string, ErBox> { ["P"] = B(0, 0), ["C"] = B(256, 0) }, Opts));

        Assert.Equal([new ErPoint(256, 58), new ErPoint(208, 58), new ErPoint(208, 38), new ErPoint(160, 38)], edge.Points);
        Assert.Equal(edge.Points[0], edge.FromAnchor);
        Assert.Equal(edge.Points[^1], edge.ToAnchor);
    }

    [Fact]
    public void ReversedOrder_ChildLeavesItsRightSide()
    {
        var model = M(["P", "C"], R("C", "P"));
        var edge = Assert.Single(ErRouter.Route(model, new Dictionary<string, ErBox> { ["P"] = B(256, 0), ["C"] = B(0, 0) }, Opts));

        Assert.Equal([new ErPoint(160, 58), new ErPoint(208, 58), new ErPoint(208, 38), new ErPoint(256, 38)], edge.Points);
    }

    [Fact]
    public void OverlappingX_BothEndsUseTheRightSides()
    {
        var model = M(["P", "C"], R("C", "P"));
        var edge = Assert.Single(ErRouter.Route(model, new Dictionary<string, ErBox> { ["P"] = B(0, 0), ["C"] = B(40, 200) }, Opts));

        Assert.Equal([new ErPoint(200, 258), new ErPoint(248, 258), new ErPoint(248, 38), new ErPoint(160, 38)], edge.Points);
    }

    [Fact]
    public void SelfReferences_LoopsDoNotCoincide()
    {
        var model = M(["T"], R("T", "T"), R("T", "T", ["c2"], ["c0"]));
        var edges = ErRouter.Route(model, new Dictionary<string, ErBox> { ["T"] = B(0, 0) }, Opts);

        Assert.Equal(2, edges.Count);
        Assert.Equal(208, edges[0].Points[1].X);
        Assert.Equal(228, edges[1].Points[1].X);
        Assert.Equal(new ErPoint(160, 58), edges[0].FromAnchor);
        Assert.Equal(new ErPoint(160, 38), edges[0].ToAnchor);
    }

    [Fact]
    public void CompositeKey_UsesTheFirstColumn()
    {
        var model = M(["P", "C"], R("C", "P", ["c2", "c1"], ["c1", "c0"]));
        var edge = Assert.Single(ErRouter.Route(model, new Dictionary<string, ErBox> { ["P"] = B(0, 0), ["C"] = B(256, 0) }, Opts));

        Assert.Equal(78, edge.FromAnchor.Y);
        Assert.Equal(58, edge.ToAnchor.Y);
    }

    [Fact]
    public void UnknownColumn_FallsBackToTheHeaderCentre()
    {
        var model = M(["P", "C"], R("C", "P", ["nope"]));
        var edge = Assert.Single(ErRouter.Route(model, new Dictionary<string, ErBox> { ["P"] = B(0, 0), ["C"] = B(256, 0) }, Opts));

        Assert.Equal(14, edge.FromAnchor.Y);
    }

    [Fact]
    public void TwoEdgesToTheSameParentRow_GetDifferentMidX()
    {
        var model = M(["P", "X", "Y"], R("X", "P"), R("Y", "P"));
        var edges = ErRouter.Route(model, new Dictionary<string, ErBox> { ["P"] = B(0, 0), ["X"] = B(256, 0), ["Y"] = B(256, 200) }, Opts);

        Assert.Equal(208, edges[0].Points[1].X);
        Assert.Equal(212, edges[1].Points[1].X);
    }

    [Fact]
    public void MissingBox_SkipsTheEdge()
    {
        var model = M(["P", "C", "D"], R("C", "P"), R("D", "P"));
        var edges = ErRouter.Route(model, new Dictionary<string, ErBox> { ["P"] = B(0, 0), ["D"] = B(256, 0) }, Opts);

        Assert.Equal("D", Assert.Single(edges).Relation.FromKey);
    }
}
