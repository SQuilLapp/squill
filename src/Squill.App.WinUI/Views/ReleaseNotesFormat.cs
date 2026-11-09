using Squill.Presentation.Updates;

namespace Squill.App.WinUI.Views;

/// <summary>Decisions of the release notes rendering that do not need a UI thread.</summary>
internal static class ReleaseNotesFormat
{
    /// <summary>Gets the glyph drawn before each bullet item.</summary>
    public const string BulletGlyph = "•";

    /// <summary>Gets the address a link run may open.</summary>
    /// <param name="run">The run to open.</param>
    /// <returns>The absolute <c>http</c> or <c>https</c> address, or <see langword="null"/> for any other run or scheme.</returns>
    public static Uri? LinkTarget(NotesRun run) =>
        run.Style == NotesRunStyle.Link
        && Uri.TryCreate(run.Url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? uri
            : null;

    /// <summary>Counts the runs of every paragraph and bullet.</summary>
    /// <param name="blocks">The parsed notes.</param>
    /// <returns>The total number of runs; headings have none.</returns>
    public static int RunCount(IEnumerable<NotesBlock> blocks) =>
        blocks.Sum(block => block switch
        {
            NotesParagraph paragraph => paragraph.Runs.Count,
            NotesBullet bullet => bullet.Runs.Count,
            _ => 0,
        });
}
