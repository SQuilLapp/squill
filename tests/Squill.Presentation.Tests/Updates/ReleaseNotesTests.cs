using System.Text;
using Squill.Presentation.Updates;
using Xunit;

namespace Squill.Presentation.Tests.Updates;

public sealed class ReleaseNotesTests
{
    private const string Changelog010 = """
        ### Added

        - SQL client for SQLite, PostgreSQL, MySQL / MariaDB and SQL Server, built on .NET 11 with Native AOT.
        - SQL editor with syntax highlighting, formatting, snippets and `@parameter` prompts, plus an integrated terminal.
        - Inline data editing on plain `SELECT` results: edits are previewed as SQL and committed in one transaction.
        - Query history.
        """;

    private static NotesRun Plain(string text) => new(text, NotesRunStyle.Plain);

    private static IReadOnlyList<NotesRun> RunsOf(NotesBlock block) => block switch
    {
        NotesParagraph p => p.Runs,
        NotesBullet b => b.Runs,
        _ => throw new InvalidOperationException("Not a text block."),
    };

    [Fact]
    public void Parse_SeededChangelogBody_GivesHeadingAndBullets()
    {
        var blocks = ReleaseNotes.Parse(Changelog010);

        Assert.Equal(5, blocks.Count);
        Assert.Equal(new NotesHeading("Added"), blocks[0]);
        Assert.All(blocks.Skip(1), b => Assert.IsType<NotesBullet>(b));
        Assert.Equal(
            [
                Plain("SQL editor with syntax highlighting, formatting, snippets and "),
                new NotesRun("@parameter", NotesRunStyle.Code),
                Plain(" prompts, plus an integrated terminal."),
            ],
            RunsOf(blocks[2]));
        Assert.Equal([Plain("Query history.")], RunsOf(blocks[4]));
    }

    [Fact]
    public void Parse_InlineStyles()
    {
        var runs = RunsOf(ReleaseNotes.Parse("a **b** `c` [d](https://x.test/p) e").Single());

        Assert.Equal(
            [
                Plain("a "),
                new NotesRun("b", NotesRunStyle.Bold),
                Plain(" "),
                new NotesRun("c", NotesRunStyle.Code),
                Plain(" "),
                new NotesRun("d", NotesRunStyle.Link, "https://x.test/p"),
                Plain(" e"),
            ],
            runs);
    }

    [Fact]
    public void Parse_NestedMarkers_KeepBackticksInsideBold()
    {
        var runs = RunsOf(ReleaseNotes.Parse("**a `b`**").Single());

        Assert.Equal([new NotesRun("a `b`", NotesRunStyle.Bold)], runs);
    }

    [Theory]
    [InlineData("**open")]
    [InlineData("`open")]
    [InlineData("[open](https://x.test")]
    [InlineData("[open] (https://x.test)")]
    [InlineData("a ** b")]
    public void Parse_UnclosedMarkers_StayPlain(string input) =>
        Assert.Equal([Plain(input)], RunsOf(ReleaseNotes.Parse(input).Single()));

    [Theory]
    [InlineData("https://x.test/a", true)]
    [InlineData("http://x.test/a", true)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("file:///c:/x", false)]
    [InlineData("relative/path", false)]
    [InlineData("ms-settings:", false)]
    public void Parse_LinkSchemes(string url, bool isLink)
    {
        var runs = RunsOf(ReleaseNotes.Parse($"[t]({url})").Single());

        var run = Assert.Single(runs);
        if (isLink)
        {
            Assert.Equal(new NotesRun("t", NotesRunStyle.Link, url), run);
        }
        else
        {
            Assert.Equal(Plain($"t ({url})"), run);
        }
    }

    [Fact]
    public void Parse_Crlf_MatchesLf()
    {
        const string Lf = "## Title\n\nfirst\nsecond\n\n- one\n  more\n- two";

        Assert.Equal(
            ReleaseNotes.Parse(Lf),
            ReleaseNotes.Parse(Lf.Replace("\n", "\r\n", StringComparison.Ordinal)),
            new BlockComparer());
    }

    [Fact]
    public void Parse_ParagraphsAndBulletContinuations_AreJoined()
    {
        var blocks = ReleaseNotes.Parse("one\ntwo\n\nthree\n- item\n  goes on\n* star");

        NotesBlock[] expected =
        [
            new NotesParagraph([Plain("one two")]),
            new NotesParagraph([Plain("three")]),
            new NotesBullet([Plain("item goes on")]),
            new NotesBullet([Plain("star")]),
        ];

        Assert.Equal(expected, blocks, new BlockComparer());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  \n \r\n")]
    public void Parse_EmptyInput_GivesNoBlocks(string? input) => Assert.Empty(ReleaseNotes.Parse(input));

    [Fact]
    public void Parse_HugeInput_IsCutAtAFullLine()
    {
        var line = new string('x', 99) + "\n";
        var blocks = ReleaseNotes.Parse(string.Concat(Enumerable.Repeat(line, 1100)));

        // 100 KiB input, 64 KiB limit: all lines are consecutive text, so one paragraph shorter than the input.
        var text = RunsOf(Assert.Single(blocks)).Single().Text;
        Assert.True(text.Length < 64 * 1024);
        Assert.True(text.Length > 60 * 1024);
        Assert.All(text.Split(' '), word => Assert.Equal(99, word.Length));
    }

    [Fact]
    public void Parse_TableRowAndHtml_StayPlainText()
    {
        var runs = RunsOf(ReleaseNotes.Parse("| a | b |\n<b>bold</b>").Single());

        Assert.Equal([Plain("| a | b | <b>bold</b>")], runs);
    }

    [Fact]
    public void Parse_RandomPrintableInput_NeverThrowsAndLinksAreHttp()
    {
        const string Alphabet = "ab *`[]()#-\n:/.hstp";
        var random = new Random(12345);
        for (var n = 0; n < 2_000; n++)
        {
            var text = new StringBuilder();
            for (var k = random.Next(0, 120); k > 0; k--)
            {
                text.Append(Alphabet[random.Next(Alphabet.Length)]);
            }

            var blocks = ReleaseNotes.Parse(text.ToString());

            foreach (var run in blocks.Where(b => b is not NotesHeading).SelectMany(RunsOf).Where(r => r.Style == NotesRunStyle.Link))
            {
                Assert.True(Uri.TryCreate(run.Url, UriKind.Absolute, out var uri));
                Assert.True(uri.Scheme is "http" or "https");
            }
        }
    }

    private sealed class BlockComparer : IEqualityComparer<NotesBlock>
    {
        public bool Equals(NotesBlock? x, NotesBlock? y) => (x, y) switch
        {
            (NotesParagraph a, NotesParagraph b) => a.Runs.SequenceEqual(b.Runs),
            (NotesBullet a, NotesBullet b) => a.Runs.SequenceEqual(b.Runs),
            _ => object.Equals(x, y),
        };

        public int GetHashCode(NotesBlock obj) => 0;
    }
}
