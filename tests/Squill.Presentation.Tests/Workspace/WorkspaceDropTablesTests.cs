using Squill.Core;
using Squill.Core.Transactions;
using Squill.Data;
using Squill.Platform;
using Squill.Platform.Profiles;
using Squill.Platform.Settings;
using Squill.Platform.Ssh;
using Squill.Platform.Storage;
using Squill.Presentation.Connections;
using Squill.Presentation.Editor;
using Squill.Presentation.Localization;
using Squill.Presentation.Sessions;
using Squill.Presentation.Workspace;
using Xunit;

namespace Squill.Presentation.Tests.Workspace;

public sealed class WorkspaceDropTablesTests : IDisposable
{
    private readonly FakeDialogService _dialogs = new();
    private readonly UiLanguageScope _lang = new();

    private static TableInfo T(string name, params ForeignKeyInfo[] fks) =>
        new(null, name, TableKind.Table, [new ColumnDefinition("id", "integer", false, true, null)], [], fks);

    private static ForeignKeyInfo Fk(string table) => new("fk", ["x"], null, table, ["id"]);

    private sealed class UiLanguageScope : IDisposable
    {
        private readonly IDisposable _inner = UiText.Override(UiLanguage.English);
        public void Dispose() => _inner.Dispose();
    }

    public void Dispose() => _lang.Dispose();

    private async Task<(WorkspaceViewModel Workspace, Provider Provider)> StartAsync(
        DatabaseDialect dialect, ConnectionEnvironment env, params TableInfo[] tables)
    {
        var provider = new Provider(dialect) { Schema = new SchemaSnapshot(null, tables) };
        var registry = new ProviderRegistry();
        registry.Register(provider);
        var store = new Store();
        var workspace = new WorkspaceViewModel(
            new SessionManager(registry, store, new Tunnels()),
            new ConnectionListViewModel(store, registry, _dialogs),
            new History(),
            new Settings(),
            new ImmediateDispatcher(),
            _dialogs,
            () => new StubSqlEditor(),
            store);
        var profile = new ConnectionProfile(Guid.NewGuid(), "p", dialect, "h", 1, "shop", "u", null, null, env);
        workspace.ConnectionList.Profiles.Add(profile);
        await workspace.ConnectCommand.ExecuteAsync(profile);
        return (workspace, provider);
    }

    [Fact]
    public async Task Production_single_table_asks_the_production_confirmation_and_declined_runs_nothing()
    {
        var (workspace, provider) = await StartAsync(DatabaseDialect.PostgreSql, ConnectionEnvironment.Production, T("users"));
        _dialogs.ProductionResult = new(false, false);

        await workspace.DropTableCommand.ExecuteAsync(workspace.ActiveSession!.Schema.Tables[0]);

        var request = Assert.Single(_dialogs.ProductionRequests);
        Assert.Contains(request.Statements, s => s.Text.Contains("DROP TABLE", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(_dialogs.Confirms);
        Assert.Empty(provider.Executed);
        workspace.Dispose();
    }

    [Fact]
    public async Task Production_requires_typing_the_database_name()
    {
        var (workspace, _) = await StartAsync(DatabaseDialect.PostgreSql, ConnectionEnvironment.Production, T("a"), T("b"));

        await workspace.DropTablesCommand.ExecuteAsync(workspace.ActiveSession!.Schema.Tables.ToList());

        Assert.Equal("shop", Assert.Single(_dialogs.ProductionRequests).RequiredText);
        workspace.Dispose();
    }

    [Fact]
    public async Task Referencing_tables_are_dropped_before_the_tables_they_reference()
    {
        var clients = T("clients");
        var orders = T("commandes", Fk("clients"));
        var lines = T("lignes", Fk("commandes"));
        var (workspace, provider) = await StartAsync(DatabaseDialect.PostgreSql, ConnectionEnvironment.None, clients, orders, lines);

        await workspace.DropTablesCommand.ExecuteAsync(workspace.ActiveSession!.Schema.Tables.ToList());

        var drops = provider.Executed.Where(s => s.StartsWith("DROP", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.Equal(3, drops.Count);
        Assert.Contains("lignes", drops[0], StringComparison.Ordinal);
        Assert.Contains("commandes", drops[1], StringComparison.Ordinal);
        Assert.Contains("clients", drops[2], StringComparison.Ordinal);
        Assert.Equal("Drop tables", Assert.Single(_dialogs.Confirms).Title);
        workspace.Dispose();
    }

    [Fact]
    public async Task A_blocker_outside_the_selection_reports_and_sends_nothing()
    {
        var (workspace, provider) = await StartAsync(DatabaseDialect.PostgreSql, ConnectionEnvironment.None,
            T("clients"), T("commandes", Fk("clients")));

        await workspace.DropTablesCommand.ExecuteAsync(new[] { workspace.ActiveSession!.Schema.Tables.Single(t => t.Name == "clients") });

        var error = Assert.Single(_dialogs.Errors);
        Assert.Contains("commandes -> clients", error.Message, StringComparison.Ordinal);
        Assert.Empty(provider.Executed);
        Assert.Empty(_dialogs.Confirms);
        workspace.Dispose();
    }

    [Fact]
    public async Task Declined_plain_confirmation_runs_nothing()
    {
        var (workspace, provider) = await StartAsync(DatabaseDialect.PostgreSql, ConnectionEnvironment.None, T("a"), T("b"));
        _dialogs.ConfirmResult = false;

        await workspace.DropTablesCommand.ExecuteAsync(workspace.ActiveSession!.Schema.Tables.ToList());

        Assert.Empty(provider.Executed);
        workspace.Dispose();
    }

    [Fact]
    public async Task PostgreSql_failure_on_the_third_statement_rolls_everything_back()
    {
        var (workspace, provider) = await StartAsync(DatabaseDialect.PostgreSql, ConnectionEnvironment.None, T("a"), T("b"), T("c"));
        provider.FailWhen = s => s.Contains("\"c\"", StringComparison.Ordinal) && s.StartsWith("DROP", StringComparison.OrdinalIgnoreCase);

        await workspace.DropTablesCommand.ExecuteAsync(workspace.ActiveSession!.Schema.Tables.ToList());

        Assert.Equal("BEGIN", provider.Executed[0]);
        Assert.Equal("ROLLBACK", provider.Executed[^1]);
        Assert.DoesNotContain("COMMIT", provider.Executed);
        Assert.Equal("Drop failed", Assert.Single(_dialogs.Errors).Title);
        workspace.Dispose();
    }

    [Fact]
    public async Task PostgreSql_success_commits_the_own_transaction()
    {
        var (workspace, provider) = await StartAsync(DatabaseDialect.PostgreSql, ConnectionEnvironment.None, T("a"), T("b"));

        await workspace.DropTablesCommand.ExecuteAsync(workspace.ActiveSession!.Schema.Tables.ToList());

        Assert.Equal("BEGIN", provider.Executed[0]);
        Assert.Equal("COMMIT", provider.Executed[^1]);
        Assert.Empty(_dialogs.Errors);
        workspace.Dispose();
    }

    [Fact]
    public async Task MySql_stops_at_the_first_failure_and_tells_what_was_dropped()
    {
        var (workspace, provider) = await StartAsync(DatabaseDialect.MySql, ConnectionEnvironment.None, T("a"), T("b"), T("c"));
        provider.FailWhen = s => s.Contains("`b`", StringComparison.Ordinal);

        await workspace.DropTablesCommand.ExecuteAsync(workspace.ActiveSession!.Schema.Tables.ToList());

        Assert.Single(provider.Executed);
        Assert.Contains("`a`", provider.Executed[0], StringComparison.Ordinal);
        Assert.DoesNotContain(provider.Executed, s => s.StartsWith("BEGIN", StringComparison.OrdinalIgnoreCase));
        var error = Assert.Single(_dialogs.Errors);
        Assert.Equal(UiText.Get("workspace.dropFailed"), error.Title);
        Assert.Contains("1", error.Message, StringComparison.Ordinal);
        Assert.Contains("b", error.Message, StringComparison.Ordinal);
        workspace.Dispose();
    }

    [Fact]
    public async Task MySql_with_an_open_transaction_asks_the_implicit_commit_question()
    {
        var (workspace, provider) = await StartAsync(DatabaseDialect.MySql, ConnectionEnvironment.None, T("a"));
        var tx = workspace.ActiveSession!.Transaction;
        await tx.SetModeAsync(TransactionMode.Manual, CancellationToken.None);
        await tx.EnsureOpenAsync(Squill.Sql.Transactions.StatementClassifier.Classify("DELETE FROM a", DatabaseDialect.MySql), CancellationToken.None);

        await workspace.DropTablesCommand.ExecuteAsync(workspace.ActiveSession!.Schema.Tables.ToList());

        Assert.Equal(UiText.Get("tx.implicitCommit.title"), _dialogs.Confirms[^1].Title);
        Assert.Contains(provider.Executed, s => s.StartsWith("DROP", StringComparison.OrdinalIgnoreCase));
        workspace.Dispose();
    }

    [Fact]
    public async Task Open_user_transaction_uses_a_savepoint_and_journals_the_statements()
    {
        var (workspace, provider) = await StartAsync(DatabaseDialect.PostgreSql, ConnectionEnvironment.None, T("a"), T("b"));
        var tx = workspace.ActiveSession!.Transaction;
        await tx.SetModeAsync(TransactionMode.Manual, CancellationToken.None);
        await tx.EnsureOpenAsync(Squill.Sql.Transactions.StatementClassifier.Classify("DELETE FROM a", DatabaseDialect.PostgreSql), CancellationToken.None);
        provider.Executed.Clear();

        await workspace.DropTablesCommand.ExecuteAsync(workspace.ActiveSession!.Schema.Tables.ToList());

        Assert.StartsWith("SAVEPOINT", provider.Executed[0], StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("BEGIN", provider.Executed);
        Assert.DoesNotContain("COMMIT", provider.Executed);
        Assert.Equal(2, tx.Journal.Count(p => p.Text.StartsWith("DROP", StringComparison.OrdinalIgnoreCase)));
        workspace.Dispose();
    }

    private sealed class Provider(DatabaseDialect dialect) : IDatabaseProvider
    {
        public DatabaseDialect Dialect => dialect;
        public SchemaSnapshot Schema { get; set; } = SchemaSnapshot.Empty;
        public Func<string, bool>? FailWhen { get; set; }
        public List<string> Executed { get; } = [];

        public ValueTask<IDatabaseConnection> OpenAsync(ConnectionProfile profile, string? password, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IDatabaseConnection>(new Connection(this));
    }

    private sealed class Connection(Provider owner) : IDatabaseConnection, ITransactionProbe
    {
        public string ServerVersion => "1";

        public ValueTask<IResultReader> ExecuteAsync(string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken)
        {
            if (owner.FailWhen?.Invoke(sql) == true)
                throw new InvalidOperationException("locked");
            owner.Executed.Add(sql);
            return ValueTask.FromResult<IResultReader>(new Reader());
        }

        public ValueTask<TransactionProbeResult> ProbeAsync(CancellationToken cancellationToken) => ValueTask.FromResult(TransactionProbeResult.Unknown);
        public ValueTask<IReadOnlyList<string>> ListDatabasesAsync(CancellationToken cancellationToken) => ValueTask.FromResult<IReadOnlyList<string>>([]);
        public ValueTask<SchemaSnapshot> ReadSchemaAsync(CancellationToken cancellationToken) => ValueTask.FromResult(owner.Schema);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class Reader : IResultReader
    {
        public IReadOnlyList<ColumnInfo> Columns { get; } = [];
        public long RecordsAffected => -1;
        public ValueTask<object?[][]?> ReadBatchAsync(int maxRows, CancellationToken cancellationToken) => ValueTask.FromResult<object?[][]?>(null);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class Store : IProfileStore
    {
        public ValueTask<IReadOnlyList<ConnectionProfile>> ListAsync(CancellationToken ct) => ValueTask.FromResult<IReadOnlyList<ConnectionProfile>>([]);
        public ValueTask SaveAsync(ConnectionProfile profile, string? password, string? sshSecret, CancellationToken ct) => ValueTask.CompletedTask;
        public ValueTask DeleteAsync(Guid id, CancellationToken ct) => ValueTask.CompletedTask;
        public ValueTask<string?> GetPasswordAsync(Guid id, CancellationToken ct) => ValueTask.FromResult<string?>(null);
        public ValueTask<string?> GetSshSecretAsync(Guid id, CancellationToken ct) => ValueTask.FromResult<string?>(null);
    }

    private sealed class Tunnels : ISshTunnelFactory
    {
        public ValueTask<ISshTunnel> OpenAsync(SshTunnelSettings settings, string? secret, string remoteHost, int remotePort, SshHostKeyPrompt? prompt, CancellationToken ct) =>
            throw new NotSupportedException();
    }

    private sealed class History : IQueryHistoryStore
    {
        public ValueTask AppendAsync(HistoryEntry e, CancellationToken ct) => ValueTask.CompletedTask;
        public ValueTask<IReadOnlyList<HistoryEntry>> ListAsync(int max, CancellationToken ct) => ValueTask.FromResult<IReadOnlyList<HistoryEntry>>([]);
        public ValueTask ClearAsync(CancellationToken ct) => ValueTask.CompletedTask;
    }

    private sealed class Settings : ISettingsStore
    {
        public ValueTask<AppSettings> LoadAsync(CancellationToken ct) => ValueTask.FromResult(AppSettings.Default);
        public ValueTask SaveAsync(AppSettings settings, CancellationToken ct) => ValueTask.CompletedTask;
    }
}
