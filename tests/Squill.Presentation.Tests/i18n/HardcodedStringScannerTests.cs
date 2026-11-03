using Xunit;

namespace Squill.Presentation.Tests.I18n;

public sealed class HardcodedStringScannerTests
{
    private static List<StringCandidate> Cs(string code, string path = "src/Squill.Presentation/X.cs") =>
        HardcodedStringScanner.ScanCs(path, code).ToList();

    private static List<StringCandidate> Xaml(string xaml) => HardcodedStringScanner.ScanXaml("src/Squill.App.WinUI/X.xaml", xaml).ToList();

    [Fact]
    public void Xaml_FindsAttributesAndElementText()
    {
        var found = Xaml("<Button Content=\"Save\" ToolTipService.ToolTip=\"Save it\"/>\n<TextBlock>Hello world</TextBlock>");
        Assert.Equal(["Save", "Save it", "Hello world"], found.Select(c => c.Text));
        Assert.All(found, c => Assert.Equal("xaml-attr", c.Kind));
        Assert.Equal([1, 1, 2], found.Select(c => c.Line));
    }

    [Fact]
    public void Xaml_SkipsBindingsGlyphsEntitiesAndComments() =>
        Assert.Empty(Xaml("<TextBlock Text=\"{Binding Name}\" Tag=\"Plain\"/><TextBlock Text=\"&#xE700;\"/><TextBlock Text=\"…\"/><TextBlock Text=\"1/2\"/>\n<!-- <Button Content=\"Hidden\"/> -->"));

    [Fact]
    public void Cs_DialogCallIsASink() =>
        Assert.Equal(("cs-sink", "Drop failed"), Single(Cs("await _dialogs.ShowErrorAsync(\"Drop failed\", ex.Message);")));

    [Fact]
    public void Cs_PropertyAssignmentIsASinkAndKeepsInterpolatedText() =>
        Assert.Equal(("cs-sink", "Connection failed: {ex.Message}"), Single(Cs("StatusText = $\"Connection failed: {ex.Message}\";")));

    [Fact]
    public void Cs_IssueConstructorIsASink() =>
        Assert.Equal(("cs-sink", "Name is required."), Single(Cs("yield return new DesignIssue(Severity.Error, \"Name is required.\");")));

    [Fact]
    public void Cs_ProseLiteralIsText() => Assert.Equal(("cs-text", "Foreign keys"), Single(Cs("var keys = Node(\"Foreign keys\", Folder);")));

    [Theory]
    [InlineData("// a comment with two words \"two words\"")]
    [InlineData("/// <summary>\"two words\"</summary>")]
    [InlineData("var s = \"two words\" + nameof(Bar);")]
    [InlineData("[Obsolete(\"two words\")]")]
    [InlineData("throw new ArgumentException(\"two words\");")]
    [InlineData("Debug.WriteLine(\"two words\");")]
    [InlineData("var q = \"SELECT 1 FROM t\";")]
    [InlineData("var e = \"SQUILL_DATA_DIR is set\";")]
    [InlineData("var k = UiText.Get(\"explorer.title\");")]
    [InlineData("var key = \"explorer.title\";")]
    public void Cs_SkipRules(string code) => Assert.Empty(Cs(code));

    [Fact]
    public void Cs_CoreMessageIsListedOnlyInCoreProjects()
    {
        Assert.Equal(("core-msg", "Name is required."), Single(Cs("return new DesignIssue(\"Name is required.\");", "src/Squill.Core/Design/V.cs")));
        Assert.Empty(Cs("var s = \"two words\";", "src/Squill.Core/Design/V.cs"));
    }

    [Theory]
    [InlineData("src/Squill.Presentation/Connections/ConnectionEditorViewModel.cs", "connections")]
    [InlineData("src/Squill.App.WinUI/MainWindow.Terminal.cs", "history-snippets-terminal")]
    [InlineData("src/Squill.App.Avalonia/Views/StatusBar.axaml", "query")]
    [InlineData("src/Squill.Presentation/Workspace/WorkspaceViewModel.cs", "settings-dialogs-errors")]
    [InlineData("src/Squill.Presentation/Explorer/ExplorerTreeViewModel.cs", "explorer-chrome")]
    [InlineData("src/Squill.Presentation/Unknown.cs", "other")]
    public void AreaOf_MapsPaths(string path, string area) => Assert.Equal(area, HardcodedStringScanner.AreaOf(path));

    [Theory]
    [InlineData("src/Squill.Data.Sqlite/A.cs")]
    [InlineData("src/Squill.App.WinUI/obj/x/A.g.cs")]
    [InlineData("src/Squill.Presentation/Localization/UiTextCatalogEn.cs")]
    [InlineData("src/Squill.App.Avalonia/Views/StyleGallery.axaml")]
    public void IsExcluded_SkipsNonUiFiles(string path) => Assert.True(HardcodedStringScanner.IsExcluded(path));

    private static (string Kind, string Text) Single(List<StringCandidate> found)
    {
        var c = Assert.Single(found);
        return (c.Kind, c.Text);
    }
}
