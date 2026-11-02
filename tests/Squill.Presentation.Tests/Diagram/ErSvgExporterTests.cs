using System.Xml.Linq;
using Squill.Core;
using Squill.Presentation.Diagram;
using Xunit;

namespace Squill.Presentation.Tests.Diagram;

public sealed class ErSvgExporterTests
{
    private static readonly XNamespace Ns = "http://www.w3.org/2000/svg";
    private static readonly ErLayoutOptions Options = new(s => s.Length * 7);

    private static ErEntity Entity(string name, params ErColumn[] cols) => new(name, null, name, TableKind.Table, cols);

    private static ErRelation Relation(bool optional, ErCardinality card = ErCardinality.ManyToOne) =>
        new("fk", "b", ["a_id"], "a", ["id"], card, optional);

    private static string Draw(ErModel model, bool dark = false)
    {
        var boxes = ErLayout.Compute(model, Options);
        return ErSvgExporter.Render(model, boxes, ErRouter.Route(model, boxes, Options), Options, dark);
    }

    private static ErModel Sample(bool optional = false, string aName = "a") => new(
        [
            Entity(aName, new ErColumn("id", "int", true, false, false)),
            Entity("b", new ErColumn("id", "int", true, false, false), new ErColumn("a_id", "int", false, true, optional)),
        ],
        [Relation(optional)],
        0);

    [Fact]
    public void Output_IsXmlWithTwoRectsPerEntityAndOnePolylinePerEdge()
    {
        var doc = XDocument.Parse(Draw(Sample()));

        Assert.Equal(Ns + "svg", doc.Root!.Name);
        Assert.Equal(4, doc.Descendants(Ns + "rect").Count());
        Assert.Single(doc.Descendants(Ns + "polyline"));
        Assert.Contains(doc.Descendants(Ns + "tspan"), t => t.Value == "PK");
        Assert.Contains(doc.Descendants(Ns + "tspan"), t => t.Value == "FK");
    }

    [Fact]
    public void Names_AreEscaped()
    {
        const string name = "a<&\"'>b";

        var doc = XDocument.Parse(Draw(Sample(aName: name)));

        Assert.Contains(doc.Descendants(Ns + "text"), t => t.Value == name);
    }

    [Fact]
    public void OptionalRelation_IsDashed()
    {
        Assert.Contains("stroke-dasharray", Draw(Sample(optional: true)));
        Assert.DoesNotContain("stroke-dasharray", Draw(Sample(optional: false)));
    }

    [Fact]
    public void Output_IsDeterministicAndThemed()
    {
        Assert.Equal(Draw(Sample()), Draw(Sample()));
        Assert.NotEqual(Draw(Sample(), dark: false), Draw(Sample(), dark: true));
    }

    [Fact]
    public void EmptyModel_IsValidSvg()
    {
        var doc = XDocument.Parse(Draw(new ErModel([], [], 0)));

        Assert.Equal("32", (string?)doc.Root!.Attribute("width"));
    }
}
