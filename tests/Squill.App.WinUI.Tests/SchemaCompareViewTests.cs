using Squill.App.WinUI.Views;
using Squill.Presentation.Compare;
using Xunit;

namespace Squill.App.WinUI.Tests;

public class SchemaCompareViewTests
{
    [Fact]
    public void Aligned_lines_give_one_row_per_side_with_the_same_count()
    {
        var lines = LineDiff.Align("a\nb\nc", "a\nx\nc\nd");

        var source = lines.Select(l => CompareFormat.Row(l, sourceSide: true)).ToList();
        var target = lines.Select(l => CompareFormat.Row(l, sourceSide: false)).ToList();

        Assert.Equal(lines.Count, source.Count);
        Assert.Equal(lines.Count, target.Count);
        Assert.Equal(3, source.Count(r => r.Number is not null));
        Assert.Equal(4, target.Count(r => r.Number is not null));
    }

    [Fact]
    public void Source_side_marks_plus_target_side_marks_minus_and_the_missing_side_is_a_hatched_filler()
    {
        var added = new AlignedLine(1, "new", null, null, LineMark.SourceOnly);
        var removed = new AlignedLine(null, null, 1, "old", LineMark.TargetOnly);
        var changed = new AlignedLine(2, "n", 2, "o", LineMark.Changed);
        var same = new AlignedLine(3, "s", 3, "s", LineMark.Same);

        Assert.Equal(DdlRowKind.Added, CompareFormat.Row(added, true).Kind);
        Assert.Equal(DdlRowKind.Filler, CompareFormat.Row(added, false).Kind);
        Assert.Equal(DdlRowKind.Filler, CompareFormat.Row(removed, true).Kind);
        Assert.Equal(DdlRowKind.Removed, CompareFormat.Row(removed, false).Kind);
        Assert.Equal(DdlRowKind.Added, CompareFormat.Row(changed, true).Kind);
        Assert.Equal(DdlRowKind.Removed, CompareFormat.Row(changed, false).Kind);
        Assert.Equal(DdlRowKind.Same, CompareFormat.Row(same, true).Kind);
        Assert.Equal("+", CompareFormat.Mark(DdlRowKind.Added));
        Assert.Equal("-", CompareFormat.Mark(DdlRowKind.Removed));
        Assert.Equal("", CompareFormat.Mark(DdlRowKind.Filler));
        Assert.Null(CompareFormat.Row(added, false).Number);
    }

    [Fact]
    public void Compare_view_builds_its_lists_in_code_and_binds_no_list_of_objects()
    {
        var code = File.ReadAllText("Xaml/SchemaCompareView.cs");

        Assert.DoesNotContain("ItemsSource", code);
        Assert.Contains("vm.Groups", code);
        Assert.Contains("VirtualKey.F7", code);
        Assert.Contains("ExecuteScriptCommand", code);
    }

    [Fact]
    public void Main_window_hosts_the_compare_tab_and_closes_it_through_its_command()
    {
        var xaml = File.ReadAllText("Xaml/MainWindow.xaml");
        var code = File.ReadAllText("Xaml/MainWindow.Diagram.cs");

        Assert.Contains("<v:SchemaCompareView x:Name=\"CompareArea\"", xaml);
        Assert.Contains("workspace.Compares.CollectionChanged", code);
    }
}
