using Squill.Core;
using Squill.Data;
using Squill.Platform;
using Squill.Platform.Profiles;
using Squill.Platform.Settings;
using Squill.Platform.Ssh;
using Squill.Platform.Storage;
using Squill.Presentation.Connections;
using Squill.Presentation.Editor;
using Squill.Presentation.Explorer;
using Squill.Presentation.Localization;
using Squill.Presentation.Sessions;
using Squill.Presentation.Workspace;
using Xunit;

namespace Squill.Presentation.Tests.Workspace;

public sealed class WorkspaceDesignerTests
{
    private static readonly TableInfo Users = new(null, "users", TableKind.Table, [new ColumnDefinition("id", "integer", false, true, null)], [], []);
    private static readonly TableInfo Recent = new(null, "recent", TableKind.View, [], [], []);

    private readonly FakeDialogService _dialogs = new();
    private readonly FakeProvider _provider = new();
    private readonly WorkspaceViewModel _workspace;

    public WorkspaceDesignerTests()
        : this(true)
    {
    }

    private WorkspaceDesignerTests(bool withDiagram)
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
            () => new StubSqlEditor(),
            store,
            diagramStore: withDiagram ? new FakeLayouts() : null,
            measureText: withDiagram ? text => text.Length * 7 : null);
    }

    private static ConnectionProfile Profile() =>
        new(Guid.NewGuid(), "p", DatabaseDialect.PostgreSql, "h", 1, "d", "u", null, null);

    private async Task ConnectAsync() => await ConnectAsync(Profile());

    private async Task ConnectAsync(ConnectionProfile profile)
    {
        _workspace.ConnectionList.Profiles.Add(profile);
        await _workspace.ConnectCommand.ExecuteAsync(profile);
    }

    [Fact]
    public async Task ActiveEnvironment_FollowsActiveSession()
    {
        Assert.Equal(ConnectionEnvironment.None, _workspace.ActiveEnvironment);

        await _workspace.ConnectCommand.ExecuteAsync(Profile() with { Environment = ConnectionEnvironment.Production });
        Assert.Equal(ConnectionEnvironment.Production, _workspace.ActiveEnvironment);

        _workspace.ActiveSession = null;
        Assert.Equal(ConnectionEnvironment.None, _workspace.ActiveEnvironment);
    }

    [Fact]
    public async Task NewTable_OpensDesignerOnActiveSession()
    {
        Assert.False(_workspace.NewTableCommand.CanExecute(null));
        await ConnectAsync();

        _workspace.NewTableCommand.Execute(null);

        Assert.Same(_workspace.ActiveSession, Assert.Single(_workspace.Designers).Session);
        Assert.Same(_workspace.Designers[0], _workspace.ActiveDesigner);
    }

    [Fact]
    public async Task DesignTable_OpenTwice_ReusesDesigner()
    {
        await ConnectAsync();

        _workspace.DesignTableCommand.Execute(Users);
        _workspace.NewTableCommand.Execute(null);
        _workspace.DesignTableCommand.Execute(Users);

        Assert.Equal(2, _workspace.Designers.Count);
        Assert.Same(_workspace.Designers[0], _workspace.ActiveDesigner);
    }

    [Fact]
    public async Task DesignTable_View_IsIgnored()
    {
        await ConnectAsync();

        _workspace.DesignTableCommand.Execute(Recent);

        Assert.Empty(_workspace.Designers);
    }

    [Fact]
    public async Task DesignerScript_OpensQueryTab()
    {
        await ConnectAsync();
        _workspace.NewTableCommand.Execute(null);
        var before = _workspace.Tabs.Count;

        _workspace.Designers[0].OpenScriptCommand.Execute(null);

        Assert.Equal(before + 1, _workspace.Tabs.Count);
    }

    [Fact]
    public async Task OpenDiagram_IsUniquePerSession()
    {
        await ConnectAsync();

        await _workspace.OpenDiagramCommand.ExecuteAsync(null);
        await _workspace.OpenDiagramCommand.ExecuteAsync(null);
        Assert.Single(_workspace.Diagrams);

        await ConnectAsync();
        await _workspace.OpenDiagramCommand.ExecuteAsync(null);
        Assert.Equal(2, _workspace.Diagrams.Count);
        Assert.Same(_workspace.Diagrams[1], _workspace.ActiveDiagram);
    }

    [Fact]
    public async Task OpenDiagram_WithoutTextMeasurer_CannotExecute()
    {
        var workspace = new WorkspaceDesignerTests(withDiagram: false);
        await workspace.ConnectAsync();

        Assert.False(workspace._workspace.OpenDiagramCommand.CanExecute(null));
    }

    [Fact]
    public async Task DiagramRequests_OpenTableAndDesigner()
    {
        _provider.Schema = new SchemaSnapshot(null, [Users]);
        await ConnectAsync();
        await _workspace.OpenDiagramCommand.ExecuteAsync(null);
        var diagram = _workspace.Diagrams[0];

        diagram.DesignCommand.Execute(diagram.Model.Entities[0].Key);
        await Task.Yield();
        diagram.ActivateCommand.Execute(diagram.Model.Entities[0].Key);

        Assert.Single(_workspace.Designers);
        Assert.Equal(2, _workspace.Tabs.Count);
    }

    [Fact]
    public async Task Disconnect_ClosesDesignersAndDiagrams()
    {
        await ConnectAsync();
        _workspace.NewTableCommand.Execute(null);
        await _workspace.OpenDiagramCommand.ExecuteAsync(null);

        await _workspace.DisconnectCommand.ExecuteAsync(null);

        Assert.Empty(_workspace.Designers);
        Assert.Empty(_workspace.Diagrams);
        Assert.Null(_workspace.ActiveDesigner);
        Assert.Null(_workspace.ActiveDiagram);
    }

    [Fact]
    public async Task CloseDesigner_WithChanges_AsksConfirmation()
    {
        using var lang = UiText.Override(UiLanguage.English);
        await ConnectAsync();
        _workspace.NewTableCommand.Execute(null);
        _dialogs.ConfirmResult = false;

        await _workspace.CloseDesignerCommand.ExecuteAsync(_workspace.Designers[0]);
        Assert.Single(_workspace.Designers);
        Assert.Equal(("Close designer", "Discard unsaved changes?"), Assert.Single(_dialogs.Confirms));

        _dialogs.ConfirmResult = true;
        await _workspace.CloseDesignerCommand.ExecuteAsync(_workspace.Designers[0]);
        Assert.Empty(_workspace.Designers);
    }

    [Fact]
    public async Task CloseDesigner_WithoutChanges_DoesNotAsk()
    {
        _provider.Schema = new SchemaSnapshot(null, [Users]);
        await ConnectAsync();
        _workspace.DesignTableCommand.Execute(Users);

        await _workspace.CloseDesignerCommand.ExecuteAsync(_workspace.Designers[0]);

        Assert.Empty(_workspace.Designers);
        Assert.Empty(_dialogs.Confirms);
    }

    [Fact]
    public async Task CloseDiagram_DisposesAndRemoves()
    {
        await ConnectAsync();
        await _workspace.OpenDiagramCommand.ExecuteAsync(null);

        _workspace.CloseDiagramCommand.Execute(_workspace.Diagrams[0]);

        Assert.Empty(_workspace.Diagrams);
        Assert.Null(_workspace.ActiveDiagram);
    }

    [Fact]
    public async Task DropTable_Confirmed_DropsRefreshesAndClosesDesigner()
    {
        using var lang = UiText.Override(UiLanguage.English);
        _provider.Schema = new SchemaSnapshot(null, [Users]);
        await ConnectAsync();
        _workspace.DesignTableCommand.Execute(Users);
        _provider.Schema = SchemaSnapshot.Empty;

        await _workspace.DropTableCommand.ExecuteAsync(Users);

        Assert.Equal(("Drop table", "Drop users? This cannot be undone."), Assert.Single(_dialogs.Confirms));
        Assert.Contains(_provider.Executed, s => s.Contains("DROP TABLE", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(_workspace.ActiveSession!.Schema.Tables);
        Assert.Empty(_workspace.Designers);
    }

    [Fact]
    public async Task DropTable_Declined_DoesNothing()
    {
        await ConnectAsync();
        _dialogs.ConfirmResult = false;

        await _workspace.DropTableCommand.ExecuteAsync(Users);

        Assert.Empty(_provider.Executed);
    }

    [Fact]
    public async Task DropTable_Failing_ShowsErrorAndKeepsDesigner()
    {
        using var lang = UiText.Override(UiLanguage.English);
        _provider.Schema = new SchemaSnapshot(null, [Users]);
        await ConnectAsync();
        _workspace.DesignTableCommand.Execute(Users);
        _provider.ExecuteFailure = new InvalidOperationException("locked");

        await _workspace.DropTableCommand.ExecuteAsync(Users);

        Assert.Equal(("Drop failed", "locked"), Assert.Single(_dialogs.Errors));
        Assert.Single(_workspace.Designers);
    }

    [Fact]
    public async Task ExplorerDesign_OpensDesignerInWorkspace()
    {
        _provider.Schema = new SchemaSnapshot(null, [Users]);
        await ConnectAsync();
        var table = Descendants(_workspace.Explorer.Roots[0]).Single(n => n.Kind == ExplorerNodeKind.Table);

        _workspace.Explorer.DesignCommand.Execute(table);

        Assert.Single(_workspace.Designers);
    }

    [Fact]
    public async Task SelectingANodeOfAnotherConnection_MakesItsSessionActive()
    {
        var first = Profile();
        var second = Profile();
        await ConnectAsync(first);
        await ConnectAsync(second);
        var firstSession = _workspace.Sessions[0];
        Assert.Same(_workspace.Sessions[1], _workspace.ActiveSession);

        _workspace.Explorer.SelectedNode = _workspace.Explorer.Roots.Single(r => r.Profile == first);

        Assert.Same(firstSession, _workspace.ActiveSession);
    }

    private static IEnumerable<ExplorerNode> Descendants(ExplorerNode node) => node.Children.SelectMany(c => Descendants(c).Prepend(c));

    private sealed class FakeLayouts : IDiagramLayoutStore
    {
        public ValueTask<DiagramLayout?> LoadAsync(Guid profileId, CancellationToken ct) => ValueTask.FromResult<DiagramLayout?>(null);
        public ValueTask SaveAsync(Guid profileId, DiagramLayout layout, CancellationToken ct) => ValueTask.CompletedTask;
        public ValueTask DeleteAsync(Guid profileId, CancellationToken ct) => ValueTask.CompletedTask;
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
        public SchemaSnapshot Schema { get; set; } = SchemaSnapshot.Empty;
        public Exception? ExecuteFailure { get; set; }
        public List<string> Executed { get; } = [];

        public ValueTask<IDatabaseConnection> OpenAsync(ConnectionProfile profile, string? password, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IDatabaseConnection>(new FakeConnection(this));
    }

    private sealed class FakeConnection(FakeProvider owner) : IDatabaseConnection
    {
        public string ServerVersion => "1";

        public ValueTask<IResultReader> ExecuteAsync(string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken)
        {
            if (owner.ExecuteFailure is { } failure)
                throw failure;
            owner.Executed.Add(sql);
            return ValueTask.FromResult<IResultReader>(new FakeReader());
        }

        public ValueTask<IReadOnlyList<string>> ListDatabasesAsync(CancellationToken cancellationToken) => ValueTask.FromResult<IReadOnlyList<string>>([]);

        public ValueTask<SchemaSnapshot> ReadSchemaAsync(CancellationToken cancellationToken) => ValueTask.FromResult(owner.Schema);

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
