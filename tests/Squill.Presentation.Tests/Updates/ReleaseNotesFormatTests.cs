using Squill.Presentation.Updates;
using Xunit;

namespace Squill.Presentation.Tests.Updates;

public class ReleaseNotesFormatTests
{
    private const string Sample = "### Added\n- **Fast** open of [the docs](https://example.com/docs) and `code`\n\nA paragraph with [bad](javascript:alert(1)) link.\n";

    [Fact]
    public void Sample_notes_have_one_heading_one_bullet_and_one_paragraph()
    {
        var blocks = ReleaseNotes.Parse(Sample);

        Assert.Single(blocks.OfType<NotesHeading>());
        Assert.Single(blocks.OfType<NotesBullet>());
        Assert.Single(blocks.OfType<NotesParagraph>());
        Assert.True(ReleaseNotesFormat.RunCount(blocks) >= 6);
    }

    [Theory]
    [InlineData("https://example.com/a", true)]
    [InlineData("http://example.com/a", true)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("file:///c:/windows", false)]
    [InlineData("ms-settings:", false)]
    [InlineData("not a url", false)]
    [InlineData(null, false)]
    public void Only_absolute_http_and_https_links_can_be_opened(string? url, bool opens) =>
        Assert.Equal(opens, ReleaseNotesFormat.LinkTarget(new NotesRun("text", NotesRunStyle.Link, url)) is not null);

    [Fact]
    public void Run_that_is_not_a_link_has_no_target() =>
        Assert.Null(ReleaseNotesFormat.LinkTarget(new NotesRun("text", NotesRunStyle.Plain, "https://example.com")));

    [Fact]
    public void Links_of_the_sample_keep_only_the_https_one()
    {
        var runs = ReleaseNotes.Parse(Sample).SelectMany(b => b switch
        {
            NotesParagraph p => p.Runs,
            NotesBullet bl => bl.Runs,
            _ => [],
        });

        var targets = runs.Select(ReleaseNotesFormat.LinkTarget).Where(t => t is not null).ToList();

        Assert.Single(targets);
        Assert.Equal("https://example.com/docs", targets[0]!.AbsoluteUri);
    }
}
