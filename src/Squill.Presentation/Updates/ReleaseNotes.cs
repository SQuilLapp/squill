using System.Text;

namespace Squill.Presentation.Updates;

/// <summary>Parses the markdown subset used by <c>CHANGELOG.md</c> into blocks every head can render without a markdown or HTML control.</summary>
public static class ReleaseNotes
{
    private const int MaxLength = 64 * 1024;

    /// <summary>Parses release notes; anything outside the subset is kept as plain text and nothing is dropped.</summary>
    /// <param name="markdown">The markdown, or <see langword="null"/>.</param>
    /// <returns>The blocks in order; empty for null or blank input. Input over 64 KiB is cut at the last full line before the limit.</returns>
    public static IReadOnlyList<NotesBlock> Parse(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return [];
        }

        var blocks = new List<NotesBlock>();
        var paragraph = new StringBuilder();
        var bullet = new StringBuilder();
        var inBullet = false;

        void FlushParagraph()
        {
            if (paragraph.Length > 0)
            {
                blocks.Add(new NotesParagraph(ParseInline(paragraph.ToString())));
                paragraph.Clear();
            }
        }

        void FlushBullet()
        {
            if (inBullet)
            {
                blocks.Add(new NotesBullet(ParseInline(bullet.ToString())));
                bullet.Clear();
                inBullet = false;
            }
        }

        foreach (var raw in Truncate(markdown).Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            var text = line.Trim();
            if (text.Length == 0)
            {
                FlushParagraph();
                FlushBullet();
            }
            else if (line[0] == '#' && line.TrimStart('#').Trim() is { Length: > 0 } heading)
            {
                FlushParagraph();
                FlushBullet();
                blocks.Add(new NotesHeading(heading));
            }
            else if (line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal))
            {
                FlushParagraph();
                FlushBullet();
                inBullet = true;
                bullet.Append(line[2..].Trim());
            }
            else if (inBullet && line.StartsWith("  ", StringComparison.Ordinal))
            {
                bullet.Append(' ').Append(text);
            }
            else
            {
                FlushBullet();
                paragraph.Append(paragraph.Length > 0 ? " " : string.Empty).Append(text);
            }
        }

        FlushParagraph();
        FlushBullet();
        return blocks;
    }

    private static string Truncate(string markdown)
    {
        if (markdown.Length <= MaxLength)
        {
            return markdown;
        }

        var cut = markdown.LastIndexOf('\n', MaxLength - 1);
        return markdown[..(cut >= 0 ? cut : MaxLength)];
    }

    private static List<NotesRun> ParseInline(string text)
    {
        var runs = new List<NotesRun>();
        var plain = new StringBuilder();
        var i = 0;

        void Emit(NotesRun run)
        {
            if (plain.Length > 0)
            {
                runs.Add(new NotesRun(plain.ToString(), NotesRunStyle.Plain));
                plain.Clear();
            }

            runs.Add(run);
        }

        while (i < text.Length)
        {
            var consumed = TryStyled(text, i, out var run, out var literal);
            if (consumed > 0)
            {
                if (run is not null)
                {
                    Emit(run);
                }
                else
                {
                    plain.Append(literal);
                }

                i += consumed;
            }
            else
            {
                plain.Append(text[i]);
                i++;
            }
        }

        if (plain.Length > 0)
        {
            runs.Add(new NotesRun(plain.ToString(), NotesRunStyle.Plain));
        }

        return runs;
    }

    /// <summary>Recognizes a styled span at <paramref name="start"/>; returns the characters consumed, or 0 when there is none.</summary>
    private static int TryStyled(string text, int start, out NotesRun? run, out string? literal)
    {
        run = null;
        literal = null;
        switch (text[start])
        {
            case '*' when start + 1 < text.Length && text[start + 1] == '*':
                var boldEnd = text.IndexOf("**", start + 2, StringComparison.Ordinal);
                if (boldEnd > start + 2)
                {
                    run = new NotesRun(text[(start + 2)..boldEnd], NotesRunStyle.Bold);
                    return boldEnd + 2 - start;
                }

                return 0;
            case '`':
                var codeEnd = text.IndexOf('`', start + 1);
                if (codeEnd > start + 1)
                {
                    run = new NotesRun(text[(start + 1)..codeEnd], NotesRunStyle.Code);
                    return codeEnd + 1 - start;
                }

                return 0;
            case '[':
                return TryLink(text, start, out run, out literal);
            default:
                return 0;
        }
    }

    private static int TryLink(string text, int start, out NotesRun? run, out string? literal)
    {
        run = null;
        literal = null;
        var close = text.IndexOf(']', start + 1);
        if (close < 0 || close + 1 >= text.Length || text[close + 1] != '(')
        {
            return 0;
        }

        var end = text.IndexOf(')', close + 2);
        if (end < 0)
        {
            return 0;
        }

        var label = text[(start + 1)..close];
        var url = text[(close + 2)..end].Trim();
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
        {
            run = new NotesRun(label.Length > 0 ? label : url, NotesRunStyle.Link, url);
        }
        else
        {
            literal = $"{label} ({url})";
        }

        return end + 1 - start;
    }
}
