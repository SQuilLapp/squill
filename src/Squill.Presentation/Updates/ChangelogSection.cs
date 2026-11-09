namespace Squill.Presentation.Updates;

/// <summary>Cuts one version's section out of <c>CHANGELOG.md</c> with the rules of <c>eng/release-notes.ps1</c>.</summary>
public static class ChangelogSection
{
    private const string Placeholder = "_Nothing yet._";

    /// <summary>Extracts the body of <c>## [version]</c>, falling back to <c>Unreleased</c> when the version has no section.</summary>
    /// <param name="changelog">The changelog text, or <see langword="null"/>.</param>
    /// <param name="version">The version whose notes are wanted.</param>
    /// <returns>The section body without its heading; empty when there is none, it is blank or it only holds the placeholder.</returns>
    public static string Extract(string? changelog, string version)
    {
        if (string.IsNullOrWhiteSpace(changelog))
        {
            return string.Empty;
        }

        var lines = changelog.Split('\n').Select(l => l.TrimEnd()).ToArray();
        var start = Find(lines, version);
        if (start < 0)
        {
            start = Find(lines, "Unreleased");
        }

        if (start < 0)
        {
            return string.Empty;
        }

        var body = lines.Skip(start).TakeWhile(l => !l.StartsWith("## ", StringComparison.Ordinal)).ToList();
        var text = string.Join('\n', body).Trim('\n');
        return text.Trim() == Placeholder ? string.Empty : text;
    }

    private static int Find(string[] lines, string version) =>
        Array.FindIndex(lines, l => l.StartsWith("## [" + version + "]", StringComparison.Ordinal)) is var i and >= 0 ? i + 1 : -1;
}
