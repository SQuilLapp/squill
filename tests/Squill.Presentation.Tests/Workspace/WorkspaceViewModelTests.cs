using Squill.Core;
using Squill.Data;
using Squill.Platform;
using Squill.Platform.Profiles;
using Squill.Platform.Settings;
using Squill.Platform.Ssh;
using Squill.Platform.Storage;
using Squill.Presentation.Connections;
using Squill.Presentation.Editor;
using Squill.Presentation.Sessions;
using Squill.Presentation.Workspace;
using Xunit;

namespace Squill.Presentation.Tests.Workspace;

public sealed class WorkspaceViewModelTests
{
    private static readonly TableInfo Users = new(null, "users", TableKind.Table, [], [], []);

    private readonly FakeDialogService _dialogs = new();
    private readonly FakeProvider _provider = new();
    private readonly List<StubSqlEditor> _editors = [];
    private readonly WorkspaceViewModel _workspace;

    public WorkspaceViewModelTests()
    {
        var registry = new ProviderRegistry();
        registry.Register(_provider);
        var store = new FakeStore();
        _workspace = new WorkspaceViewModel(
            new SessionManager(registry, store, new FakeTunnels()),
            new ConnectionListViewModel(store, registry, _dialogs),
            new FakeHistory(),
            new FakeSettings(),
            new ImmediateDispatcher(),
            _dialogs,
            () =>
            {
                var editor = new StubSqlEditor();
                _editors.Add(editor);
                return editor;
            });
    }

    private static ConnectionProfile Profile() =>
        new(Guid.NewGuid(), "p", DatabaseDialect.PostgreSql, "h", 1, "d", "u", null, null);

    private async Task ConnectAsync() => await _workspace.ConnectCommand.ExecuteAsync(Profile());

    [Fact]
    public async Task Connect_Succeeds_CreatesExplorerAndFirstTab()
    {
        await ConnectAsync();

        Assert.NotNull(_workspace.ActiveSession);
        Assert.NotNull(_workspace.Explorer);
        Assert.Equal("Query 1", Assert.Single(_workspace.Tabs).Title);
        Assert.Same(_workspace.Tabs[0], _workspace.ActiveTab);
    }

    [Fact]
    public async Task Connect_Fails_ShowsError()
    {
        _provider.Failure = new InvalidOperationException("nope");

        await ConnectAsync();

        Assert.Null(_workspace.ActiveSession);
        Assert.Equal("nope", Assert.Single(_dialogs.Errors).Message);
    }

    [Fact]
    public async Task NewTab_AddsUniqueTitles()
    {
        await ConnectAsync();

        _workspace.NewTabCommand.Execute(null);

        Assert.Equal(["Query 1", "Query 2"], _workspace.Tabs.Select(t => t.Title));
    }

    [Fact]
    public async Task CloseTab_Modified_AsksConfirmation()
    {
        await ConnectAsync();
        _editors[0].Text = "SELECT 1";
        _dialogs.ConfirmResult = false;

        await _workspace.CloseTabCommand.ExecuteAsync(_workspace.Tabs[0]);
        Assert.Single(_workspace.Tabs);

        _dialogs.ConfirmResult = true;
        await _workspace.CloseTabCommand.ExecuteAsync(_workspace.Tabs[0]);
        Assert.Empty(_workspace.Tabs);
        Assert.Null(_workspace.ActiveTab);
    }

    [Fact]
    public async Task CloseTab_Unmodified_ClosesWithoutAsking()
    {
        await ConnectAsync();
        _dialogs.ConfirmResult = false;

        await _workspace.CloseTabCommand.ExecuteAsync(_workspace.Tabs[0]);

        Assert.Empty(_workspace.Tabs);
    }

    [Fact]
    public async Task OpenTable_CreatesTabAndRunsSelectTop()
    {
        await ConnectAsync();

        await _workspace.OpenTableCommand.ExecuteAsync(Users);

        var expected = SqlSnippets.SelectTop(Users, DatabaseDialect.PostgreSql);
        Assert.Equal(expected, _editors[1].Text);
        Assert.Equal([expected], _provider.Executed);
        Assert.False(_workspace.ActiveTab!.IsModified);
    }

    [Fact]
    public async Task Disconnect_ClosesSessionAndItsTabs()
    {
        await ConnectAsync();
        _workspace.NewTabCommand.Execute(null);

        await _workspace.DisconnectCommand.ExecuteAsync(null);

        Assert.Empty(_workspace.Tabs);
        Assert.Empty(_workspace.Sessions);
        Assert.Null(_workspace.ActiveSession);
        Assert.Null(_workspace.Explorer);
    }

    [Fact]
    public void AppCompositionCreate_TempDirectory_BuildsWorkspace()
    {
        var dir = Path.Combine(Path.GetTempPath(), "squill-" + Guid.NewGuid().ToString("N"));
        try
        {
            var services = AppComposition.Create(new Platform(new ProviderRegistry(), _dialogs), new AppPaths(dir));

            Assert.NotNull(services.Workspace.ConnectionList);
        }
        finally
        {
            try
            {
                Directory.Delete(dir, true);
            }
            catch (IOException)
            {
            }
        }
    }

    private sealed class Platform(ProviderRegistry providers, IDialogService dialogs) : IPlatformServices
    {
        public IUiDispatcher Dispatcher { get; } = new ImmediateDispatcher();
        public IDialogService Dialogs { get; } = dialogs;
        public Func<ISqlEditor> EditorFactory { get; } = () => new StubSqlEditor();
        public ProviderRegistry Providers { get; } = providers;
    }

    private sealed class FakeStore : IProfileStore
    {
        public ValueTask<IReadOnlyList<ConnectionProfile>> ListAsync(CancellationToken ct) => ValueTask.FromResult<IReadOnlyList<ConnectionProfile>>([]);
        public ValueTask SaveAsync(ConnectionProfile profile, string? password, string? sshSecret, CancellationToken ct) => ValueTask.CompletedTask;
        public ValueTask DeleteAsync(Guid id, CancellationToken ct) => ValueTask.CompletedTask;
        public ValueTask<string?> GetPasswordAsync(Guid id, CancellationToken ct) => ValueTask.FromResult<string?>(null);
        public ValueTask<string?> GetSshSecretAsync(Guid id, CancellationToken ct) => ValueTask.FromResult<string?>(null);
    }

    private sealed class FakeTunnels : ISshTunnelFactory
    {
        public ValueTask<ISshTunnel> OpenAsync(SshTunnelSettings settings, string? secret, string remoteHost, int remotePort, CancellationToken ct) =>
            throw new NotSupportedException();
    }

    private sealed class FakeHistory : IQueryHistoryStore
    {
        public ValueTask AppendAsync(HistoryEntry e, CancellationToken ct) => ValueTask.CompletedTask;
        public ValueTask<IReadOnlyList<HistoryEntry>> ListAsync(int max, CancellationToken ct) => ValueTask.FromResult<IReadOnlyList<HistoryEntry>>([]);
        public ValueTask ClearAsync(CancellationToken ct) => ValueTask.CompletedTask;
    }

    private sealed class FakeSettings : ISettingsStore
    {
        public ValueTask<AppSettings> LoadAsync(CancellationToken ct) => ValueTask.FromResult(AppSettings.Default);
        public ValueTask SaveAsync(AppSettings settings, CancellationToken ct) => ValueTask.CompletedTask;
    }

    private sealed class FakeProvider : IDatabaseProvider
    {
        public DatabaseDialect Dialect => DatabaseDialect.PostgreSql;
        public Exception? Failure { get; set; }
        public List<string> Executed { get; } = [];

        public ValueTask<IDatabaseConnection> OpenAsync(ConnectionProfile profile, string? password, CancellationToken cancellationToken) =>
            Failure is { } failure ? throw failure : ValueTask.FromResult<IDatabaseConnection>(new FakeConnection(this));
    }

    private sealed class FakeConnection(FakeProvider owner) : IDatabaseConnection
    {
        public string ServerVersion => "1";

        public ValueTask<IResultReader> ExecuteAsync(string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken)
        {
            owner.Executed.Add(sql);
            return ValueTask.FromResult<IResultReader>(new FakeReader());
        }

        public ValueTask<SchemaSnapshot> ReadSchemaAsync(CancellationToken cancellationToken) => ValueTask.FromResult(SchemaSnapshot.Empty);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeReader : IResultReader
    {
        public IReadOnlyList<ColumnInfo> Columns { get; } = [];
        public long RecordsAffected => -1;

        public ValueTask<object?[][]?> ReadBatchAsync(int maxRows, CancellationToken cancellationToken) => ValueTask.FromResult<object?[][]?>(null);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
