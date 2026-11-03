using Squill.App.WinUI.Controls;
using Squill.Presentation.Diagram;
using Xunit;

namespace Squill.App.WinUI.Tests;

public class ErDiagramGeometryTests
{
    private static readonly ErEdge Edge = new(
        new ErRelation("fk", "a", ["x"], "b", ["y"], ErCardinality.ManyToOne, false),
        [new ErPoint(0, 0), new ErPoint(100, 0), new ErPoint(100, 50)],
        new ErPoint(0, 0),
        new ErPoint(100, 50));

    [Theory]
    [InlineData(50, 4, true)]
    [InlineData(50, 6, false)]
    [InlineData(103, 25, true)]
    [InlineData(-4, 0, true)]
    [InlineData(-8, 0, false)]
    [InlineData(40, 40, false)]
    public void Near_follows_every_segment_of_the_edge(double x, double y, bool near) =>
        Assert.Equal(near, ErDiagramGeometry.Near(Edge, new ErPoint(x, y), 5));

    [Theory]
    [InlineData(1.0, "100 %")]
    [InlineData(0.854, "85 %")]
    [InlineData(0.1, "10 %")]
    public void Zoom_is_drawn_as_a_rounded_percentage(double zoom, string text) =>
        Assert.Equal(text, ErDiagramGeometry.ZoomText(zoom));
}
