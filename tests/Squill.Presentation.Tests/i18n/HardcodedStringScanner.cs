using System.Text.RegularExpressions;

namespace Squill.Presentation.Tests.I18n;

/// <summary>A user-visible string that is still hard-coded: where it is, what kind of place it sits in, and its text.</summary>
public sealed record StringCandidate(string Path, int Line, string Kind, string Text);

/// <summary>Finds user-visible string literals in <c>src</c> that are not in the localization catalog.</summary>
public static partial class HardcodedStringScanner
{
    private const string XamlAttributes =
        @"ToolTipService\.ToolTip|ToolTip\.Tip|ToolTip|AutomationProperties\.Name|AutomationProperties\.HelpText|PrimaryButtonText|SecondaryButtonText|CloseButtonText|PlaceholderText|Placeholder|Watermark|Description|Content|Header|Title|Label|Text";

    [GeneratedRegex("(?<![\\w.:])(?:" + XamlAttributes + ")=\"([^\"{][^\"]*)\"")]
    private static partial Regex XamlAttribute();

    [GeneratedRegex(@">\s*([^<>{&]*?\p{L}[^<>{&]*?)\s*</(?:TextBlock|Run|Button|CheckBox|RadioButton|MenuItem|ToggleButton|HyperlinkButton|Label)>")]
    private static partial Regex XamlElementText();

    [GeneratedRegex(@"&#?\w+;")]
    private static partial Regex XmlEntity();

    [GeneratedRegex(@"\$@""(?:[^""]|"""")*""|@\$?""(?:[^""]|"""")*""|\$?""(?:[^""\\]|\\.)*""")]
    private static partial Regex Literal();

    [GeneratedRegex(@"\{[^{}]*\}")]
    private static partial Regex InterpolationHole();

    [GeneratedRegex(@"\b(?:ShowErrorAsync|ConfirmAsync|PromptAsync|ShowInfoAsync)\s*\([^;]*$")]
    private static partial Regex DialogCall();

    [GeneratedRegex(@"\b(?:StatusText|Title|Message|SaveError|Error|Tooltip|Text|Header|Label|Description|Placeholder)\s*(?:=>|(?<![=!<>])=(?!=)|\+=|\?\?=)[^;]*$")]
    private static partial Regex SinkAssignment();

    [GeneratedRegex(@"new\s+(?:DesignIssue|EditIssue)\s*\([^;]*$")]
    private static partial Regex IssueConstructor();

    [GeneratedRegex(@"new\s+(?:\w*(?:Issue|Diagnostic)|Message)\s*\([^;]*$")]
    private static partial Regex CoreMessageConstructor();

    [GeneratedRegex(@"^(?:SELECT|INSERT|UPDATE|DELETE|CREATE|ALTER|DROP|PRAGMA|WITH)\b")]
    private static partial Regex SqlStart();

    [GeneratedRegex(@"^\p{L}.*(?:\S\s+\S|[.!?:]$)")]
    private static partial Regex ProseLike();

    [GeneratedRegex(@"nameof\(|^\s*\[|throw new Argument|\bDebug\.|\bTrace\.|GeneratedRegex|SQUILL_|UiText\.")]
    private static partial Regex SkippedLine();

    // First match wins, order matters.
    private static readonly (string Needle, string Area)[] Areas =
    [
        ("/Connections/", "connections"), ("ConnectionEditor", "connections"),
        ("/Panels/", "history-snippets-terminal"), ("HistoryView", "history-snippets-terminal"), ("HistoryFormat", "history-snippets-terminal"),
        ("SnippetsView", "history-snippets-terminal"), ("SqlSnippets", "history-snippets-terminal"), ("Terminal", "history-snippets-terminal"),
        ("SearchBox", "history-snippets-terminal"),
        ("/Designer/", "designer-er"), ("/Diagram/", "designer-er"), ("TableDesigner", "designer-er"), ("DiagramTab", "designer-er"),
        ("ErDiagram", "designer-er"), ("DesignIssue", "designer-er"), ("MainWindow.Diagram", "designer-er"),
        ("/Results/", "results"), ("/Editing/", "results"), ("DataGrid", "results"), ("EditBarFormat", "results"), ("GridEdit", "results"),
        ("/Query/", "query"), ("/Editor/", "query"), ("/Editors/", "query"), ("QueryTab", "query"), ("StatusBar", "query"),
        ("ParameterPromptRow", "query"), ("SqlColorizer", "query"), ("SqlRunner", "query"),
        ("/Settings/", "settings-dialogs-errors"), ("SettingsWindow", "settings-dialogs-errors"), ("SettingsFormat", "settings-dialogs-errors"),
        ("DialogService", "settings-dialogs-errors"), ("AppComposition", "settings-dialogs-errors"),
        ("Program.cs", "settings-dialogs-errors"), ("/Workspace/", "settings-dialogs-errors"), ("/Sessions/", "settings-dialogs-errors"),
        ("/Explorer/", "explorer-chrome"), ("ObjectExplorer", "explorer-chrome"), ("Sidebar", "explorer-chrome"), ("PropertiesPanel", "explorer-chrome"),
        ("PropertiesModel", "explorer-chrome"), ("MainWindow", "explorer-chrome"), ("ChromeFormat", "explorer-chrome"), ("TabHeader", "explorer-chrome"),
        ("TabInfo", "explorer-chrome"), ("WorkspaceTabList", "explorer-chrome"), ("App.axaml", "explorer-chrome"), ("ExplorerGlyphs", "explorer-chrome"),
    ];

    /// <summary>Walks up from the test binaries to the repository root (the folder holding <c>Squill.slnx</c>).</summary>
    public static string FindRepositoryRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "Squill.slnx")))
            dir = Path.GetDirectoryName(dir);
        return dir ?? throw new InvalidOperationException("Squill.slnx not found above " + AppContext.BaseDirectory);
    }

    /// <summary>Migration area of a repository-relative path (see the area table); <c>other</c> when none matches.</summary>
    public static string AreaOf(string path)
    {
        foreach (var (needle, area) in Areas)
            if (path.Contains(needle, StringComparison.Ordinal))
                return area;
        return "other";
    }

    /// <summary>Head of a repository-relative path: <c>avalonia</c>, <c>winui</c> or <c>shared</c>.</summary>
    public static string HeadOf(string path) =>
        path.Contains("Squill.App.Avalonia", StringComparison.Ordinal) ? "avalonia"
        : path.Contains("Squill.App.WinUI", StringComparison.Ordinal) ? "winui"
        : "shared";

    /// <summary>Scans the whole repository and drops the candidates of the ignore list, sorted by area, path, line.</summary>
    public static IReadOnlyList<StringCandidate> ScanRepository(string root)
    {
        var ignored = LoadIgnored(Path.Combine(root, "tests", "Squill.Presentation.Tests", "i18n", "ignored-literals.txt"));
        var result = new List<StringCandidate>();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "src"), "*.*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (IsExcluded(rel))
                continue;
            var text = File.ReadAllText(file);
            var found = rel.EndsWith(".cs", StringComparison.Ordinal) ? ScanCs(rel, text)
                : rel.EndsWith(".xaml", StringComparison.Ordinal) || rel.EndsWith(".axaml", StringComparison.Ordinal) ? ScanXaml(rel, text)
                : [];
            result.AddRange(found.Where(c => !ignored.Contains((c.Path, c.Text))));
        }

        return result.OrderBy(c => AreaOf(c.Path), StringComparer.Ordinal)
            .ThenBy(c => c.Path, StringComparer.Ordinal).ThenBy(c => c.Line).ToList();
    }

    /// <summary>True for generated code, build output, developer tools and projects without UI.</summary>
    public static bool IsExcluded(string rel) =>
        rel.Contains("/bin/", StringComparison.Ordinal) || rel.Contains("/obj/", StringComparison.Ordinal)
        || rel.Contains("Localization/UiTextCatalog", StringComparison.Ordinal)
        || rel.EndsWith(".g.cs", StringComparison.Ordinal) || rel.EndsWith(".Designer.cs", StringComparison.Ordinal)
        || rel.StartsWith("src/Squill.DesignSystem", StringComparison.Ordinal) || rel.StartsWith("src/Squill.Data", StringComparison.Ordinal)
        || rel.Contains("/Bench/", StringComparison.Ordinal) || rel.Contains("StyleGallery", StringComparison.Ordinal);

    /// <summary>Finds candidates in XAML: text-bearing attributes and element text.</summary>
    public static IEnumerable<StringCandidate> ScanXaml(string path, string content)
    {
        var lines = content.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.TrimStart().StartsWith("<!--", StringComparison.Ordinal))
                continue;
            foreach (Match m in XamlAttribute().Matches(line))
                if (HasLetter(m.Groups[1].Value))
                    yield return new(path, i + 1, "xaml-attr", m.Groups[1].Value);
            foreach (Match m in XamlElementText().Matches(line))
                if (HasLetter(m.Groups[1].Value))
                    yield return new(path, i + 1, "xaml-attr", m.Groups[1].Value);
        }
    }

    /// <summary>Finds candidates in C#: dialog and property sinks, prose-like literals, and core messages.</summary>
    public static IEnumerable<StringCandidate> ScanCs(string path, string content)
    {
        var coreOnly = path.StartsWith("src/Squill.Core/", StringComparison.Ordinal) || path.StartsWith("src/Squill.Sql/", StringComparison.Ordinal)
            || path.StartsWith("src/Squill.Platform/", StringComparison.Ordinal);
        var lines = content.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Replace("'\"'", "' '").Replace("'\\\"'", "' '");
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("//", StringComparison.Ordinal) || trimmed.StartsWith('*') || trimmed.StartsWith("/*", StringComparison.Ordinal))
                continue;
            foreach (Match m in Literal().Matches(line))
            {
                var raw = m.Value;
                var inner = raw[(raw.IndexOf('"') + 1)..^1];
                var plain = raw.Contains('$', StringComparison.Ordinal) ? InterpolationHole().Replace(inner, "") : inner;
                var prefix = line[..m.Index];
                var context = i > 0 && lines[i - 1].TrimEnd() is { } p && (p.EndsWith('(') || p.EndsWith(',')) ? lines[i - 1] + " " + prefix : prefix;

                if (coreOnly)
                {
                    if (CoreMessageConstructor().IsMatch(context) && HasLetter(plain))
                        yield return new(path, i + 1, "core-msg", inner);
                    continue;
                }

                if (SkippedLine().IsMatch(line))
                    continue;
                if ((DialogCall().IsMatch(context) || SinkAssignment().IsMatch(context) || IssueConstructor().IsMatch(context)) && HasLetter(plain))
                    yield return new(path, i + 1, "cs-sink", inner);
                else if (ProseLike().IsMatch(plain.Trim()) && !SqlStart().IsMatch(plain.Trim()))
                    yield return new(path, i + 1, "cs-text", inner);
            }
        }
    }

    private static bool HasLetter(string value) => XmlEntity().Replace(value, "").Any(char.IsLetter);

    private static HashSet<(string Path, string Text)> LoadIgnored(string file)
    {
        var set = new HashSet<(string, string)>();
        if (!File.Exists(file))
            return set;
        foreach (var line in File.ReadAllLines(file))
        {
            if (line.Length == 0 || line.StartsWith('#'))
                continue;
            var parts = line.Split('|');
            if (parts.Length >= 3)
                set.Add((parts[0], parts[1]));
        }

        return set;
    }
}
