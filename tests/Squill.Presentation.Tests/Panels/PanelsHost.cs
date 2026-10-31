using Squill.Core;
using Squill.Data;
using Squill.Platform.Profiles;
using Squill.Platform.Settings;
using Squill.Platform.Ssh;
using Squill.Platform.Storage;
using Squill.Presentation.Connections;
using Squill.Presentation.Editor;
using Squill.Presentation.Sessions;
using Squill.Presentation.Terminal;
using Squill.Presentation.Tests.Terminal;
using Squill.Presentation.Workspace;

namespace Squill.Presentation.Tests.Panels;

/// <summary>A workspace wired to in-memory fakes, with one connected tab on demand.</summary>
internal sealed class PanelsHost
{
    public FakeDialogService Dialogs { get; } = new();
    public List<StubSqlEditor> Editors { get; } = [];
    public List<FakeTerminalHost> TerminalHosts { get; } = [];
    public WorkspaceViewModel Workspace { get; }

    public PanelsHost()
    {
        var registry = new ProviderRegistry();
        registry.Register(new FakeProvider());
        var store = new FakeProfiles();
        Workspace = new WorkspaceViewModel(
            new SessionManager(registry, store, new FakeTunnels()),
            new ConnectionListViewModel(store, registry, Dialogs),
            new FakeHistory(),
            new FakeSettings(),
            new ImmediateDispatcher(),
            Dialogs,
            () =>
            {
                var editor = new StubSqlEditor();
                Editors.Add(editor);
                return editor;
            },
            store,
            () =>
            {
                var host = new FakeTerminalHost();
                TerminalHosts.Add(host);
                return host;
            },
            name => name == "psql" ? "/usr/bin/psql" : null);
    }

    public async Task ConnectAsync() =>
        await Workspace.ConnectCommand.ExecuteAsync(
            new ConnectionProfile(Guid.NewGuid(), "p", DatabaseDialect.PostgreSql, "h", 1, "d", "u", null, null));

    private sealed class FakeProfiles : IProfileStore
    {
        public ValueTask<IReadOnlyList<ConnectionProfile>> ListAsync(CancellationToken ct) => ValueTask.FromResult<IReadOnlyList<ConnectionProfile>>([]);
        public ValueTask SaveAsync(ConnectionProfile profile, string? password, string? sshSecret, CancellationToken ct) => ValueTask.CompletedTask;
        public ValueTask DeleteAsync(Guid id, CancellationToken ct) => ValueTask.CompletedTask;
        public ValueTask<string?> GetPasswordAsync(Guid id, CancellationToken ct) => ValueTask.FromResult<string?>("secret");
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

        public ValueTask<IDatabaseConnection> OpenAsync(ConnectionProfile profile, string? password, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IDatabaseConnection>(new FakeConnection());
    }

    private sealed class FakeConnection : IDatabaseConnection
    {
        public string ServerVersion => "1";

        public ValueTask<IResultReader> ExecuteAsync(string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask<SchemaSnapshot> ReadSchemaAsync(CancellationToken cancellationToken) => ValueTask.FromResult(SchemaSnapshot.Empty);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
