using Squill.Core;
using Squill.Sql.Analysis;
using Squill.Sql.Completion;
using Squill.Data;
using Squill.Platform.Profiles;
using Squill.Platform.Storage;
using Squill.Presentation.Editor;
using Squill.Presentation.Localization;
using Squill.Presentation.Palette;
using Squill.Presentation.Sessions;
using Xunit;

namespace Squill.Presentation.Tests.Palette;

public sealed class PaletteHistoryProviderTests
{
    internal sealed class FakeEditor(string text = "") : ISqlEditor
    {
        public string Text { get; set; } = text;
        public int CaretOffset { get; set; }
        public TextRange Selection { get; set; }
        public event EventHandler? TextChanged { add { } remove { } }
        public event EventHandler? CaretChanged { add { } remove { } }
        public void SetDialect(DatabaseDialect d) { }
        public void SetDiagnostics(IReadOnlyList<SqlDiagnostic> diagnostics) { }
        public void SetCompletionProvider(ISqlCompletionProvider provider) { }
        public void SetHoverProvider(Func<SqlRequest, HoverInfo?> provider) { }
        public void FocusEditor() { }
    }

    internal sealed class FakeHost : IPaletteEditorHost
    {
        public List<ConnectionSession> Open { get; } = [];
        public ISqlEditor? Editor { get; set; }
        public List<(ConnectionSession Session, string Text)> Scripts { get; } = [];
        public IReadOnlyList<ConnectionSession> Sessions => Open;
        public ConnectionSession? ActiveSession => Open.FirstOrDefault();
        public ISqlEditor? ActiveEditor => Editor;
        public ValueTask OpenTableAsync(ConnectionSession session, TableInfo table) => ValueTask.CompletedTask;
        public void OpenScript(ConnectionSession session, string text) => Scripts.Add((session, text));
    }

    private sealed class FakeStore(params HistoryEntry[] entries) : IQueryHistoryStore
    {
        public int Reads { get; private set; }
        public ValueTask AppendAsync(HistoryEntry e, CancellationToken ct) => ValueTask.CompletedTask;
        public ValueTask ClearAsync(CancellationToken ct) => ValueTask.CompletedTask;

        public ValueTask<IReadOnlyList<HistoryEntry>> ListAsync(int max, CancellationToken ct)
        {
            Reads++;
            return ValueTask.FromResult<IReadOnlyList<HistoryEntry>>(entries);
        }
    }

    private sealed class FakeProfiles(params ConnectionProfile[] profiles) : IProfileStore
    {
        public ValueTask<IReadOnlyList<ConnectionProfile>> ListAsync(CancellationToken ct) => ValueTask.FromResult<IReadOnlyList<ConnectionProfile>>(profiles);
        public ValueTask SaveAsync(ConnectionProfile profile, string? password, string? sshSecret, CancellationToken ct) => throw new NotSupportedException();
        public ValueTask DeleteAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public ValueTask<string?> GetPasswordAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public ValueTask<string?> GetSshSecretAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class FakeClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private sealed class NoConnection : IDatabaseConnection
    {
        public int Statements { get; private set; }
        public string ServerVersion => "1";

        public ValueTask<IResultReader> ExecuteAsync(string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken)
        {
            Statements++;
            throw new NotSupportedException();
        }

        public ValueTask<IReadOnlyList<string>> ListDatabasesAsync(CancellationToken cancellationToken) => ValueTask.FromResult<IReadOnlyList<string>>([]);
        public ValueTask<SchemaSnapshot> ReadSchemaAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static readonly DateTimeOffset Now = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);
    private static readonly ConnectionProfile Profile = new(Guid.NewGuid(), "ventes", DatabaseDialect.PostgreSql, "h", 1, "db", "u", null, null, ConnectionEnvironment.Production);
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static HistoryEntry Entry(string sql, TimeSpan age, bool ok = true, Guid? profile = null) =>
        new(Now - age, profile ?? Profile.Id, sql, ok, 1, TimeSpan.FromMilliseconds(18));

    private static PaletteHistoryProvider Provider(FakeStore store, FakeHost host, params ConnectionProfile[] profiles) =>
        new(store, new FakeProfiles(profiles), host, new FakeClock(Now));

    private static ConnectionSession Session(NoConnection? connection = null) =>
        new(Profile, connection ?? new NoConnection(), new SchemaSnapshot("public", []), null);

    [Fact]
    public async Task Search_EmptyTerm_MostRecentFirst()
    {
        var store = new FakeStore(Entry("SELECT 1", TimeSpan.FromHours(3)), Entry("SELECT 2", TimeSpan.FromMinutes(5)));

        var items = await Provider(store, new FakeHost(), Profile).Search("", Ct);

        Assert.Equal(["SELECT 2", "SELECT 1"], items.Select(i => i.Title));
    }

    [Fact]
    public async Task Search_Term_KeepsOnlyMatches()
    {
        var store = new FakeStore(Entry("SELECT * FROM orders", TimeSpan.FromMinutes(1)), Entry("DELETE FROM x", TimeSpan.FromMinutes(2)));

        var items = await Provider(store, new FakeHost(), Profile).Search("orders", Ct);

        Assert.Single(items);
        Assert.NotEmpty(items[0].Ranges);
    }

    [Fact]
    public async Task Search_MultiLineSql_IsCollapsedAndCut()
    {
        var store = new FakeStore(Entry(new string('x', 300), TimeSpan.FromMinutes(2)), Entry("SELECT a,\r\n   b\n\tFROM t", TimeSpan.FromMinutes(1)));

        var items = await Provider(store, new FakeHost(), Profile).Search("", Ct);

        Assert.Equal("SELECT a, b FROM t", items[0].Title);
        Assert.Equal(120, items[1].Title.Length);
    }

    [Theory]
    [InlineData(UiLanguage.French, "il y a 12 min · 18 ms", 12)]
    [InlineData(UiLanguage.English, "12 min ago · 18 ms", 12)]
    [InlineData(UiLanguage.English, "just now · 18 ms", 0)]
    [InlineData(UiLanguage.English, "3 h ago · 18 ms", 180)]
    [InlineData(UiLanguage.English, "2 days ago · 18 ms", 2 * 24 * 60)]
    public async Task Search_RelativeTime_UsesLanguage(UiLanguage language, string expected, int minutes)
    {
        using var scope = UiText.Override(language);
        var store = new FakeStore(Entry("SELECT 1", TimeSpan.FromMinutes(minutes)));

        var items = await Provider(store, new FakeHost(), Profile).Search("", Ct);

        Assert.Equal(expected, items[0].Right);
    }

    [Fact]
    public async Task Search_FailedEntry_ShowsErrorWord()
    {
        using var scope = UiText.Override(UiLanguage.French);
        var store = new FakeStore(Entry("SELECT 1", TimeSpan.FromMinutes(3), ok: false));

        var items = await Provider(store, new FakeHost(), Profile).Search("", Ct);

        Assert.Equal("il y a 3 min · erreur", items[0].Right);
    }

    [Fact]
    public async Task Search_ProfileKnownOrNot_FillsConnectionAndEnvironment()
    {
        var store = new FakeStore(Entry("SELECT 1", TimeSpan.FromMinutes(1)), Entry("SELECT 2", TimeSpan.FromMinutes(2), profile: Guid.NewGuid()));

        var items = await Provider(store, new FakeHost(), Profile).Search("", Ct);

        Assert.Equal(["", "ventes"], items.Select(i => i.Detail).Order());
        Assert.Equal(ConnectionEnvironment.Production, items.Single(i => i.Detail == "ventes").Environment);
        Assert.Null(items.Single(i => i.Detail == "").Environment);
    }

    [Fact]
    public async Task Search_ManyTimes_ReadsStoreOncePerReload()
    {
        var store = new FakeStore(Entry("SELECT 1", TimeSpan.FromMinutes(1)));
        var provider = Provider(store, new FakeHost(), Profile);

        await provider.Search("s", Ct);
        await provider.Search("se", Ct);
        Assert.Equal(1, store.Reads);
        await provider.ReloadAsync(Ct);
        Assert.Equal(2, store.Reads);
    }

    [Theory]
    [InlineData("", "SELECT 1", 0)]
    [InlineData("abc", "abc\n\nSELECT 1", 5)]
    public async Task Run_Default_AppendsToActiveEditorWithoutExecuting(string existing, string expected, int caret)
    {
        var connection = new NoConnection();
        var editor = new FakeEditor(existing);
        var host = new FakeHost { Editor = editor };
        host.Open.Add(Session(connection));
        var items = await Provider(new FakeStore(Entry("SELECT 1", TimeSpan.FromMinutes(1))), host, Profile).Search("", Ct);

        await items[0].Run(PaletteRunMode.Default, Ct);

        Assert.Equal(expected, editor.Text);
        Assert.Equal(caret, editor.CaretOffset);
        Assert.Equal(0, connection.Statements);
        Assert.Empty(host.Scripts);
    }

    [Fact]
    public async Task Run_NoActiveEditor_OpensNewTabOnEntrySession()
    {
        var host = new FakeHost();
        var session = Session();
        host.Open.Add(session);
        var items = await Provider(new FakeStore(Entry("SELECT 1", TimeSpan.FromMinutes(1))), host, Profile).Search("", Ct);

        await items[0].Run(PaletteRunMode.Default, Ct);

        Assert.Equal([(session, "SELECT 1")], host.Scripts);
    }

    [Fact]
    public async Task Run_NewTab_OpensTabEvenWithEditor()
    {
        var editor = new FakeEditor("abc");
        var host = new FakeHost { Editor = editor };
        host.Open.Add(Session());
        var items = await Provider(new FakeStore(Entry("SELECT 1", TimeSpan.FromMinutes(1))), host, Profile).Search("", Ct);

        await items[0].Run(PaletteRunMode.NewTab, Ct);

        Assert.Equal("abc", editor.Text);
        Assert.Single(host.Scripts);
    }
}
