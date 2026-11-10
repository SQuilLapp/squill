using Squill.Core;
using Squill.Platform.Settings;
using Squill.Platform.Storage;
using Squill.Presentation.Editor;
using Squill.Presentation.Query;
using Squill.Presentation.Sessions;
using Squill.Sql.Completion;
using Squill.Tests.Shared;
using Xunit;

namespace Squill.Presentation.Tests.Query;

public sealed class QueryTabCompletionTests
{
    private static readonly SchemaSnapshot Schema = new("public",
        [new TableInfo("public", "users", TableKind.Table, [new ColumnDefinition("id", "int", false, true, null)], [], [])])
    {
        Routines = [new RoutineInfo("public", "my_func", RoutineKind.Function, "my_func(int)", "int", null)],
    };

    private sealed class Store : ISettingsStore
    {
        public KeywordCase Case { get; set; } = KeywordCase.Upper;

        public bool Throw { get; set; }

        public ValueTask<AppSettings> LoadAsync(CancellationToken ct)
            => Throw ? throw new IOException("boom") : ValueTask.FromResult(AppSettings.Default with { FormatterKeywordCase = Case });

        public ValueTask SaveAsync(AppSettings settings, CancellationToken ct) => ValueTask.CompletedTask;
    }

    private sealed class History : IQueryHistoryStore
    {
        public ValueTask AppendAsync(HistoryEntry e, CancellationToken ct) => ValueTask.CompletedTask;

        public ValueTask<IReadOnlyList<HistoryEntry>> ListAsync(int max, CancellationToken ct) => ValueTask.FromResult<IReadOnlyList<HistoryEntry>>([]);

        public ValueTask ClearAsync(CancellationToken ct) => ValueTask.CompletedTask;
    }

    private static (StubSqlEditor Editor, FakeClock Clock) Create(Store store)
    {
        var profile = new ConnectionProfile(Guid.NewGuid(), "p", DatabaseDialect.PostgreSql, "h", 1, "d", "u", null, null);
        var editor = new StubSqlEditor();
        var clock = new FakeClock(DateTimeOffset.UnixEpoch);
        _ = new QueryTabViewModel(new ConnectionSession(profile, null!, Schema, null), editor, new History(), store, new ImmediateDispatcher(), new FakeDialogService(), clock);
        return (editor, clock);
    }

    private static string[] Labels(StubSqlEditor editor, string marked)
    {
        var offset = marked.IndexOf('|', StringComparison.Ordinal);
        return [.. editor.CompletionProvider!.GetCompletions(new SqlRequest(marked.Remove(offset, 1), offset, DatabaseDialect.PostgreSql, null)).Select(i => i.Label)];
    }

    [Fact]
    public void FunctionsAreOfferedInASelectListButNotAtATablePosition()
    {
        var (editor, _) = Create(new Store());
        var select = Labels(editor, "select |");
        Assert.Contains("count", select, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("my_func", select);
        Assert.DoesNotContain("my_func", Labels(editor, "select * from |"));
    }

    [Fact]
    public void KeywordCaseComesFromTheStore()
    {
        var (editor, _) = Create(new Store { Case = KeywordCase.Lower });
        Assert.Contains("distinct", Labels(editor, "select |"));
    }

    [Fact]
    public void SettingIsRefreshedAfterTheStalenessWindow()
    {
        var store = new Store();
        var (editor, clock) = Create(store);
        Assert.Contains("DISTINCT", Labels(editor, "select |"));

        store.Case = KeywordCase.Lower;
        Assert.Contains("DISTINCT", Labels(editor, "select |"));

        clock.Now += TimeSpan.FromSeconds(6);
        Labels(editor, "select |"); // starts the refresh, the next popup sees it
        Assert.Contains("distinct", Labels(editor, "select |"));
    }

    [Fact]
    public void StoreErrorKeepsUpper()
    {
        var (editor, _) = Create(new Store { Throw = true });
        Assert.Contains("DISTINCT", Labels(editor, "select |"));
    }
}
