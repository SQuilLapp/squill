using Squill.Core;
using Squill.Data;
using Squill.Data.Sqlite;
using Squill.Platform.Profiles;
using Squill.Platform.Settings;
using Squill.Platform.Ssh;
using Squill.Platform.Storage;
using Squill.Presentation.Editor;
using Squill.Presentation.Query;
using Squill.Presentation.Results;
using Squill.Presentation.Sessions;
using Squill.Sql.Completion;
using Xunit;

namespace Squill.Presentation.Tests.Completion;

/// <summary>The completion path of a query tab over the SQLite sample schema: session schema, providers, editor adapter, and running what an item inserts.</summary>
public sealed class CompletionFlowTests : IAsyncLifetime
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"squill-cf-{Guid.NewGuid():N}.db");
    private SessionManager? _manager;
    private ConnectionSession? _session;

    public async ValueTask InitializeAsync()
    {
        var registry = new ProviderRegistry();
        registry.Register(new SqliteDatabaseProvider());
        _manager = new SessionManager(registry, new NoProfiles(), new NoTunnels());
        _session = await _manager.OpenAsync(new ConnectionProfile(Guid.NewGuid(), "cf", DatabaseDialect.Sqlite, null, null, null, null, _file, null), TestContext.Current.CancellationToken);
        var script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "SampleSchema", "sqlite.sql"), TestContext.Current.CancellationToken);
        await Exec(script);
        await Exec("insert into customers (name, email) values ('a', 'a@x'), ('b', 'b@x'), ('c', 'c@x'); insert into orders (customer_id, total) select id, 5 from customers");
        await _session.RefreshSchemaAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_manager is not null && _session is not null)
        {
            await _manager.CloseAsync(_session);
        }

        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(_file);
    }

    private async Task Exec(string sql)
    {
        await using var reader = await _session!.Connection.ExecuteAsync(sql, [], CancellationToken.None);
        await reader.ReadAllAsync(10, CancellationToken.None);
    }

    private (QueryTabViewModel Tab, StubSqlEditor Editor) NewTab(KeywordCase keywordCase = KeywordCase.Upper)
    {
        var editor = new StubSqlEditor();
        var tab = new QueryTabViewModel(_session!, editor, new NoHistory(), new Settings(keywordCase), new ImmediateDispatcher(), new FakeDialogService());
        return (tab, editor);
    }

    private static IReadOnlyList<CompletionItem> Complete(StubSqlEditor editor, string marked)
    {
        var offset = marked.IndexOf('|', StringComparison.Ordinal);
        return editor.CompletionProvider!.GetCompletions(new SqlRequest(marked.Remove(offset, 1), offset, DatabaseDialect.Sqlite, null));
    }

    [Fact]
    public async Task JoinRow_IsFirst_AndWhatItInsertsRuns()
    {
        var (tab, editor) = NewTab();
        const string marked = "select * from customers c join |";
        var items = Complete(editor, marked);
        var first = items[0];
        Assert.Equal("orders o", first.Label);
        Assert.Equal("ON c.id = o.customer_id", first.Detail);
        Assert.Equal("orders", first.FilterText);

        var text = marked.Replace("|", string.Empty, StringComparison.Ordinal);
        editor.Text = text[..first.ReplaceRange.Start] + first.InsertText + text[(first.ReplaceRange.Start + first.ReplaceRange.Length)..];
        editor.CaretOffset = editor.Text.Length;
        await tab.RunCommand.ExecuteAsync(null);
        var result = Assert.Single(tab.Results);
        Assert.Equal(ResultKind.Grid, result.Kind);
        Assert.Equal(3, result.Grid!.RowCount);
    }

    [Fact]
    public void KeywordCaseSetting_IsFollowed()
    {
        var (_, editor) = NewTab(KeywordCase.Lower);
        var labels = Complete(editor, "select * from customers c |").Select(i => i.Label).ToList();
        Assert.Contains("left join", labels);
        Assert.DoesNotContain("LEFT JOIN", labels);
    }

    [Fact]
    public void Functions_AreOfferedInASelectList()
    {
        var (_, editor) = NewTab();
        Assert.Contains(Complete(editor, "select |"), i => i.Kind == CompletionKind.Function);
    }

    [Fact]
    public async Task NewTable_AppearsInThePopupAfterRunningCreateTable()
    {
        var (tab, editor) = NewTab();
        Assert.DoesNotContain(Complete(editor, "select * from flow_|"), i => i.Label == "flow_new");
        editor.Text = "create table flow_new (id integer primary key)";
        editor.CaretOffset = 3;
        await tab.RunCommand.ExecuteAsync(null);
        Assert.Contains(Complete(editor, "select * from flow_|"), i => i.Label == "flow_new");
    }

    private sealed class Settings(KeywordCase keywordCase) : ISettingsStore
    {
        public ValueTask<AppSettings> LoadAsync(CancellationToken ct) => ValueTask.FromResult(AppSettings.Default with { FormatterKeywordCase = keywordCase });

        public ValueTask SaveAsync(AppSettings settings, CancellationToken ct) => ValueTask.CompletedTask;
    }

    private sealed class NoHistory : IQueryHistoryStore
    {
        public ValueTask AppendAsync(HistoryEntry e, CancellationToken ct) => ValueTask.CompletedTask;

        public ValueTask<IReadOnlyList<HistoryEntry>> ListAsync(int max, CancellationToken ct) => ValueTask.FromResult<IReadOnlyList<HistoryEntry>>([]);

        public ValueTask ClearAsync(CancellationToken ct) => ValueTask.CompletedTask;
    }

    private sealed class NoProfiles : IProfileStore
    {
        public ValueTask<IReadOnlyList<ConnectionProfile>> ListAsync(CancellationToken ct) => ValueTask.FromResult<IReadOnlyList<ConnectionProfile>>([]);

        public ValueTask SaveAsync(ConnectionProfile profile, string? password, string? sshSecret, CancellationToken ct) => ValueTask.CompletedTask;

        public ValueTask DeleteAsync(Guid id, CancellationToken ct) => ValueTask.CompletedTask;

        public ValueTask<string?> GetPasswordAsync(Guid id, CancellationToken ct) => ValueTask.FromResult<string?>(null);

        public ValueTask<string?> GetSshSecretAsync(Guid id, CancellationToken ct) => ValueTask.FromResult<string?>(null);
    }

    private sealed class NoTunnels : ISshTunnelFactory
    {
        public ValueTask<ISshTunnel> OpenAsync(SshTunnelSettings settings, string? secret, string remoteHost, int remotePort, CancellationToken ct) => throw new NotSupportedException();
    }
}
