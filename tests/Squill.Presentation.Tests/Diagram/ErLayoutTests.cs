using Squill.Core;
using Squill.Presentation.Diagram;
using Xunit;

namespace Squill.Presentation.Tests.Diagram;

public sealed class ErLayoutTests
{
    private static readonly ErLayoutOptions Opts = new(s => s.Length * 7);

    private static readonly string[] IsoKeys = ["a", "b", "c", "d", "e"];

    private static ErEntity E(string name, int cols = 2) =>
        new(name, null, name, TableKind.Table, [.. Enumerable.Range(0, cols).Select(i => new ErColumn("c" + i, "int", i == 0, false, false))]);

    private static ErRelation R(string from, string to) =>
        new("fk_" + from + "_" + to, from, ["c1"], to, ["c0"], ErCardinality.ManyToOne, false);

    private static ErModel M(string[] names, params (string From, string To)[] rels) =>
        new([.. names.Select(n => E(n))], [.. rels.Select(r => R(r.From, r.To))], 0);

    private static bool Overlap(ErBox a, ErBox b) =>
        a.X < b.Right && b.X < a.Right && a.Y < b.Bottom && b.Y < a.Bottom;

    private static void AssertNoOverlap(IReadOnlyDictionary<string, ErBox> boxes)
    {
        var list = boxes.ToList();
        for (var i = 0; i < list.Count; i++)
        {
            for (var j = i + 1; j < list.Count; j++)
                Assert.False(Overlap(list[i].Value, list[j].Value), $"{list[i].Key} overlaps {list[j].Key}");
        }
    }

    [Fact]
    public void Chain_ParentsAreLeftOfChildren()
    {
        var b = ErLayout.Compute(M(["A", "B", "C"], ("B", "A"), ("C", "B")), Opts);
        Assert.True(b["A"].X < b["B"].X);
        Assert.True(b["B"].X < b["C"].X);
        Assert.Equal(0, b["A"].X);
    }

    [Fact]
    public void SharedParent_IsLeftOfBothChildren()
    {
        var b = ErLayout.Compute(M(["P", "X", "Y"], ("X", "P"), ("Y", "P")), Opts);
        Assert.True(b["P"].Right < b["X"].X);
        Assert.True(b["P"].Right < b["Y"].X);
        AssertNoOverlap(b);
    }

    [Fact]
    public void Cycle_TerminatesWithDistinctBoxes()
    {
        var b = ErLayout.Compute(M(["A", "B", "C"], ("A", "B"), ("B", "C"), ("C", "A"), ("A", "A")), Opts);
        Assert.Equal(3, b.Count);
        AssertNoOverlap(b);
    }

    [Fact]
    public void SyntheticSchema_NoOverlaps()
    {
        var names = Enumerable.Range(0, 60).Select(i => "t" + i.ToString("D2", System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        List<(string, string)> rels = [];
        for (var i = 1; i < 45; i++)
        {
            rels.Add((names[i], names[(i * 7) % i]));
            if (i % 3 == 0)
                rels.Add((names[i], names[(i * 5) % i]));
        }

        var b = ErLayout.Compute(M(names, [.. rels]), Opts);
        Assert.Equal(60, b.Count);
        AssertNoOverlap(b);
        Assert.All(b.Values, v => Assert.True(v.X >= 0 && v.Y >= 0));
    }

    [Fact]
    public void Isolated_FormGridBelowComponents()
    {
        var b = ErLayout.Compute(M(["a", "b", "c", "d", "e", "p", "q"], ("q", "p")), Opts);
        var iso = IsoKeys.Select(k => b[k]).ToList();
        Assert.Equal(3, iso.Select(x => x.X).Distinct().Count()); // ceil(sqrt(5)) = 3 columns
        Assert.Equal(2, iso.Select(x => x.Y).Distinct().Count());
        Assert.All(iso, x => Assert.True(x.Y >= b["q"].Bottom));
        AssertNoOverlap(b);
    }

    [Fact]
    public void NoRelations_AllIsolated()
    {
        var b = ErLayout.Compute(M(["a", "b", "c", "d"]), Opts);
        Assert.Equal(2, b.Values.Select(x => x.X).Distinct().Count());
    }

    [Fact]
    public void Compute_IsDeterministic()
    {
        var m = M(["A", "B", "C", "D", "z"], ("B", "A"), ("C", "A"), ("D", "B"), ("D", "C"));
        Assert.Equal(ErLayout.Compute(m, Opts).OrderBy(x => x.Key), ErLayout.Compute(m, Opts).OrderBy(x => x.Key));
    }

    [Fact]
    public void Width_FollowsMeasureText()
    {
        var m = new ErModel([new ErEntity("t", null, "t", TableKind.Table,
            [new ErColumn("very_long_column_name", "varchar(255)", false, false, false)])], [], 0);
        var narrow = ErLayout.Compute(m, Opts)["T"];
        var wide = ErLayout.Compute(m, Opts with { MeasureText = s => s.Length * 14 })["t"];
        Assert.Equal(Math.Ceiling(24d + (7d * "very_long_column_name  varchar(255)".Length)), narrow.Width);
        Assert.True(wide.Width > narrow.Width);
        Assert.Equal(28 + 20, narrow.Height);
        Assert.True(narrow.Contains(new ErPoint(1, 1)));
    }
}
