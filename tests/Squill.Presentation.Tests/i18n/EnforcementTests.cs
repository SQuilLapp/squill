using System.Text.RegularExpressions;
using Squill.Core;
using Squill.Data;
using Squill.Platform.Profiles;
using Squill.Platform.Settings;
using Squill.Platform.Storage;
using Squill.Presentation.Connections;
using Squill.Presentation.Designer;
using Squill.Presentation.Editor;
using Squill.Presentation.Localization;
using Squill.Presentation.Query;
using Squill.Presentation.Sessions;
using Squill.Presentation.Settings;
using Squill.Sql;
using Xunit;

namespace Squill.Presentation.Tests.I18n;

/// <summary>Permanent guards: no user-visible literal outside the catalog, no key missing in a language.</summary>
public sealed partial class EnforcementTests(ITestOutputHelper output)
{
    private const string HowToFix = "Add a catalog key (see docs/tasks/phase-10/migration-recipe.md) or, only for non-UI text, "
                                    + "an entry with a reason in tests/Squill.Presentation.Tests/i18n/ignored-literals.txt.";

    // Keys built at run time. Each prefix is followed by a code of the listed *MessageCodes.All class.
    private static readonly (string Prefix, IReadOnlyList<string> Codes)[] DynamicKeyPrefixes =
    [
        ("issue.", [.. CoreMessageCodes.All, .. SqlMessageCodes.All]), // IssueText.Format: "issue." + code
    ];

    // UiText.Get/Format/FormatPlural/Has("k") in code, {loc:Text k} in Avalonia XAML, loc:Loc.*Key="k" in WinUI XAML, Loc.SetXxxKey(el, "k") in code.
    [GeneratedRegex(@"UiText\.(?:Get|Format|FormatPlural|Has)\(\s*""([^""]+)""|\{loc:Text\s+([\w.]+)\}|Loc\.(?:Key|ToolTipKey|HeaderKey|TitleKey|MenuTextKey|AutomationNameKey)=""([\w.]+)""|Loc\.Set\w*Key\(\s*[^,()]+,\s*""([^""]+)""")]
    private static partial Regex KeyUse();

    [GeneratedRegex(@"""([a-z][A-Za-z0-9]*(?:\.[A-Za-z0-9]+)+)""|\{loc:Text\s+([\w.]+)\}|Loc\.\w*Key=""([\w.]+)""")]
    private static partial Regex KeyLikeLiteral();

    private static readonly string Root = HardcodedStringScanner.FindRepositoryRoot();

    private static string Describe(IEnumerable<StringCandidate> found) =>
        string.Join(Environment.NewLine, found.Select(c => $"{c.Path}:{c.Line} {c.Kind} \"{c.Text}\""));

    [Fact]
    public void Scanner_FindsNoHardcodedUserText()
    {
        var found = HardcodedStringScanner.ScanRepository(Root);

        Assert.True(found.Count == 0, $"Hard-coded user-visible text found. {HowToFix}{Environment.NewLine}{Describe(found)}");
    }

    [Fact]
    public void XamlLiterals_AreBindingsOrCatalogLookups()
    {
        var found = HardcodedStringScanner.ScanRepository(Root).Where(c => c.Kind == "xaml-attr").ToList();

        Assert.True(found.Count == 0, $"XAML literal instead of a binding or a catalog lookup ({{loc:Text key}} / loc:Loc.Key). {HowToFix}{Environment.NewLine}{Describe(found)}");
    }

    [Fact]
    public void IgnoredLiterals_AreStillPresent_AndHaveReasons()
    {
        var file = Path.Combine(Root, "tests", "Squill.Presentation.Tests", "i18n", "ignored-literals.txt");
        var lines = File.ReadAllLines(file).Where(l => l.Length > 0 && !l.StartsWith('#')).ToList();
        var problems = new List<string>();

        if (!lines.SequenceEqual(lines.OrderBy(l => l, StringComparer.Ordinal)))
            problems.Add("the file is not sorted (ordinal order of whole lines)");

        foreach (var line in lines)
        {
            var parts = line.Split('|');
            if (parts.Length < 3 || parts[0].Length == 0 || parts[1].Length == 0 || string.IsNullOrWhiteSpace(string.Join('|', parts[2..])))
            {
                problems.Add($"malformed entry or empty reason: {line}");
                continue;
            }

            var path = Path.Combine(Root, parts[0]);
            if (!File.Exists(path))
                problems.Add($"file does not exist: {line}");
            else if (!File.ReadAllText(path).Contains(parts[1], StringComparison.Ordinal))
                problems.Add($"stale entry, text no longer in the file: {line}");
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    private static HashSet<string> KeysOf(IEnumerable<(string Key, string Value)> entries) => [.. entries.Select(e => e.Key)];

    private static HashSet<string> WithPluralBases(HashSet<string> keys) =>
        [.. keys, .. keys.Where(k => k.EndsWith(".one", StringComparison.Ordinal)).Select(k => k[..^4])];

    private static IEnumerable<string> SourceFiles() =>
        Directory.EnumerateFiles(Path.Combine(Root, "src"), "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".cs", StringComparison.Ordinal) || f.EndsWith(".axaml", StringComparison.Ordinal) || f.EndsWith(".xaml", StringComparison.Ordinal))
            .Where(f => !HardcodedStringScanner.IsExcluded(Path.GetRelativePath(Root, f).Replace('\\', '/')));

    [Fact]
    public void UsedKeys_ExistInBothCatalogs()
    {
        var fr = WithPluralBases(KeysOf(UiTextCatalogFr.Entries));
        var en = WithPluralBases(KeysOf(UiTextCatalogEn.Entries));
        var problems = new List<string>();
        foreach (var file in SourceFiles())
            foreach (Match m in KeyUse().Matches(File.ReadAllText(file)))
            {
                var key = m.Groups.Cast<Group>().Skip(1).First(g => g.Success).Value;
                if (!fr.Contains(key))
                    problems.Add($"{Path.GetFileName(file)}: {key} missing in French");
                if (!en.Contains(key))
                    problems.Add($"{Path.GetFileName(file)}: {key} missing in English");
            }

        foreach (var (prefix, codes) in DynamicKeyPrefixes)
            foreach (var code in codes)
            {
                if (!fr.Contains(prefix + code))
                    problems.Add($"dynamic key {prefix}{code} missing in French");
                if (!en.Contains(prefix + code))
                    problems.Add($"dynamic key {prefix}{code} missing in English");
            }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems.Distinct()));
    }

    [Fact]
    public void Catalog_HasNoUnusedKeys()
    {
        var referenced = new HashSet<string>();
        foreach (var file in SourceFiles())
            foreach (Match m in KeyLikeLiteral().Matches(File.ReadAllText(file)))
                referenced.Add(m.Groups.Cast<Group>().Skip(1).First(g => g.Success).Value);

        Assert.NotEmpty(referenced);

        // Warning level: dead keys are printed, never failing.
        var unused = UiTextCatalogEn.Entries.Select(e => e.Key)
            .Where(k => !referenced.Contains(k) && !referenced.Contains(k.EndsWith(".one", StringComparison.Ordinal) || k.EndsWith(".other", StringComparison.Ordinal) ? k[..k.LastIndexOf('.')] : k))
            .Where(k => !DynamicKeyPrefixes.Any(p => k.StartsWith(p.Prefix, StringComparison.Ordinal)))
            .ToList();
        foreach (var key in unused)
            output.WriteLine("unused catalog key: " + key);
    }

    private sealed class NoProfiles : IProfileStore
    {
        public ValueTask<IReadOnlyList<ConnectionProfile>> ListAsync(CancellationToken ct) => ValueTask.FromResult<IReadOnlyList<ConnectionProfile>>([]);

        public ValueTask SaveAsync(ConnectionProfile profile, string? password, string? sshSecret, CancellationToken ct) => ValueTask.CompletedTask;

        public ValueTask DeleteAsync(Guid id, CancellationToken ct) => ValueTask.CompletedTask;

        public ValueTask<string?> GetPasswordAsync(Guid id, CancellationToken ct) => ValueTask.FromResult<string?>(null);

        public ValueTask<string?> GetSshSecretAsync(Guid id, CancellationToken ct) => ValueTask.FromResult<string?>(null);
    }

    private sealed class NoSettings : ISettingsStore
    {
        public ValueTask<AppSettings> LoadAsync(CancellationToken ct) => ValueTask.FromResult(AppSettings.Default);

        public ValueTask SaveAsync(AppSettings settings, CancellationToken ct) => ValueTask.CompletedTask;
    }

    private sealed class NoHistory : IQueryHistoryStore
    {
        public ValueTask AppendAsync(HistoryEntry e, CancellationToken ct) => ValueTask.CompletedTask;

        public ValueTask<IReadOnlyList<HistoryEntry>> ListAsync(int max, CancellationToken ct) => ValueTask.FromResult<IReadOnlyList<HistoryEntry>>([]);

        public ValueTask ClearAsync(CancellationToken ct) => ValueTask.CompletedTask;
    }

    private sealed class NoConnection : IDatabaseConnection
    {
        public string ServerVersion => "1";

        public ValueTask<IResultReader> ExecuteAsync(string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask<IReadOnlyList<string>> ListDatabasesAsync(CancellationToken cancellationToken) => ValueTask.FromResult<IReadOnlyList<string>>([]);

        public ValueTask<SchemaSnapshot> ReadSchemaAsync(CancellationToken cancellationToken) => ValueTask.FromResult(SchemaSnapshot.Empty);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    // The user-visible strings of the main view-models, as labelled values.
    private static List<(string Name, string? Value)> VisibleStrings()
    {
        var session = new ConnectionSession(
            new ConnectionProfile(Guid.NewGuid(), "p", DatabaseDialect.PostgreSql, "h", 1, "d", "u", null, null),
            new NoConnection(), SchemaSnapshot.Empty, null);
        var dialogs = new FakeDialogService();
        var dispatcher = new ImmediateDispatcher();
        var settings = new SettingsViewModel(new NoSettings());
        var editor = new ConnectionEditorViewModel(null, new NoProfiles(), new ProviderRegistry(), dialogs);
        var designer = new TableDesignerViewModel(session, null, dialogs, dispatcher);
        var query = new QueryTabViewModel(session, new StubSqlEditor(), new NoHistory(), new NoSettings(), dispatcher, dialogs);

        List<(string, string?)> values =
        [
            ("Settings.LanguageOptions[System]", settings.LanguageOptions[0].Label),
            ("ConnectionEditor.Validate()[0]", editor.Validate()[0]),
            ("TableDesigner.Title", designer.Title),
            ("TableDesigner.StatusText", designer.StatusText),
            ("QueryTab.Title", query.Title),
            ("QueryTab.StatusText", query.StatusText),
        ];
        return values;
    }

    [Fact]
    public void TwoLanguageSmoke()
    {
        List<(string Name, string? Value)> french;
        List<(string Name, string? Value)> english;
        using (UiText.Override(UiLanguage.French))
            french = VisibleStrings();
        using (UiText.Override(UiLanguage.English))
            english = VisibleStrings();

        var problems = new List<string>();
        foreach (var ((name, fr), (_, en)) in french.Zip(english))
        {
            foreach (var (language, value) in new[] { ("fr", fr), ("en", en) })
                if (string.IsNullOrWhiteSpace(value) || (value.StartsWith('!') && value.EndsWith('!')))
                    problems.Add($"{name} is empty or a missing key in {language}: \"{value}\"");
            if (fr == en)
                problems.Add($"{name} is identical in both languages: \"{fr}\"");
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }
}
