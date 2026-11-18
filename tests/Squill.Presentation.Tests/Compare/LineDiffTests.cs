using Squill.Presentation.Compare;
using Xunit;

namespace Squill.Presentation.Tests.Compare;

public sealed class LineDiffTests
{
    [Fact]
    public void Identical_AllSame()
    {
        var rows = LineDiff.Align("a\nb\nc", "a\nb\nc");
        Assert.All(rows, r => Assert.Equal(LineMark.Same, r.Mark));
        Assert.Equal(3, rows.Count);
        Assert.Equal(3, rows[2].TargetNumber);
    }

    [Fact]
    public void OneAddedOnSourceSide_IsSourceOnlyWithTargetFiller()
    {
        var rows = LineDiff.Align("a\nx\nb", "a\nb");
        Assert.Equal([LineMark.Same, LineMark.SourceOnly, LineMark.Same], rows.Select(r => r.Mark));
        Assert.Null(rows[1].TargetNumber);
        Assert.Null(rows[1].TargetText);
        Assert.Equal(2, rows[1].SourceNumber);
        Assert.Equal(2, rows[2].TargetNumber);
        Assert.Equal(3, rows[2].SourceNumber);
    }

    [Fact]
    public void OneRemoved_IsTargetOnlyWithSourceFiller()
    {
        var rows = LineDiff.Align("a\nb", "a\nx\nb");
        Assert.Equal([LineMark.Same, LineMark.TargetOnly, LineMark.Same], rows.Select(r => r.Mark));
        Assert.Null(rows[1].SourceNumber);
        Assert.Equal("x", rows[1].TargetText);
    }

    [Fact]
    public void OneReplaced_IsChangedOnBothSides()
    {
        var rows = LineDiff.Align("a\nold\nc", "a\nnew\nc");
        Assert.Equal([LineMark.Same, LineMark.Changed, LineMark.Same], rows.Select(r => r.Mark));
        Assert.Equal("old", rows[1].SourceText);
        Assert.Equal("new", rows[1].TargetText);
    }

    [Fact]
    public void UnequalRun_PadsShorterSide()
    {
        var rows = LineDiff.Align("a\nx1\nx2\nz", "a\ny\nz");
        Assert.Equal([LineMark.Same, LineMark.Changed, LineMark.SourceOnly, LineMark.Same], rows.Select(r => r.Mark));
    }

    [Fact]
    public void TwoAddedLinesInTheMiddle_AreSourceOnlyWithFillers()
    {
        const string target = "CREATE TABLE commandes (\n  id INT,\n  total INT\n);";
        const string source = "CREATE TABLE commandes (\n  id INT,\n  statut TEXT,\n  cree_le DATE,\n  total INT\n);";
        var rows = LineDiff.Align(source, target);
        Assert.Equal(
            [LineMark.Same, LineMark.Same, LineMark.SourceOnly, LineMark.SourceOnly, LineMark.Same, LineMark.Same],
            rows.Select(r => r.Mark));
        Assert.All(rows.Skip(2).Take(2), r => Assert.Null(r.TargetNumber));
        Assert.Equal(3, rows[2].SourceNumber);
        Assert.Equal(3, rows[4].TargetNumber);
    }

    [Fact]
    public void EmptySide_IsAllOneSided()
    {
        Assert.All(LineDiff.Align("a\nb", ""), r => Assert.Equal(LineMark.SourceOnly, r.Mark));
        Assert.All(LineDiff.Align("", "a\nb"), r => Assert.Equal(LineMark.TargetOnly, r.Mark));
        Assert.Empty(LineDiff.Align("", ""));
    }

    [Fact]
    public void TrailingWhitespaceAndLineEndings_AreIgnored()
    {
        Assert.All(LineDiff.Align("a  \r\nb\t\r\n", "a\nb"), r => Assert.Equal(LineMark.Same, r.Mark));
    }

    [Fact]
    public void LargeInput_FallsBackToPositionalCompare()
    {
        var source = string.Join('\n', Enumerable.Range(0, 20_001));
        var target = "extra\n" + source;
        var rows = LineDiff.Align(source, target);
        Assert.Equal(20_002, rows.Count);
        Assert.Equal(LineMark.Changed, rows[0].Mark);
    }
}
