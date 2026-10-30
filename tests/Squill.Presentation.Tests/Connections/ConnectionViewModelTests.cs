using Squill.Core;
using Squill.Data;
using Squill.Platform.Profiles;
using Squill.Presentation.Connections;
using Xunit;

namespace Squill.Presentation.Tests.Connections;

public sealed class ConnectionViewModelTests
{
    private readonly FakeStore _store = new();
    private readonly FakeDialogService _dialogs = new();
    private readonly FakeProvider _provider = new();
    private readonly ProviderRegistry _registry = new();

    public ConnectionViewModelTests() => _registry.Register(_provider);

    private ConnectionEditorViewModel NewEditor(ConnectionProfile? existing = null) => new(existing, _store, _registry, _dialogs);

    private static ConnectionProfile Existing() =>
        new(Guid.NewGuid(), "Prod", DatabaseDialect.PostgreSql, "db", 5432, "app", "me", null, null);

    [Fact]
    public void Dialect_Changed_SwitchesDefaultPort()
    {
        var editor = NewEditor();

        Assert.Equal(5432, editor.Port);
        editor.Dialect = DatabaseDialect.MySql;
        Assert.Equal(3306, editor.Port);
        editor.Dialect = DatabaseDialect.SqlServer;
        Assert.Equal(1433, editor.Port);
        editor.Dialect = DatabaseDialect.Sqlite;
        Assert.Null(editor.Port);
    }

    [Fact]
    public void Dialect_CustomPort_IsKept()
    {
        var editor = NewEditor();
        editor.Port = 6000;

        editor.Dialect = DatabaseDialect.MySql;

        Assert.Equal(6000, editor.Port);
    }

    [Fact]
    public void Validate_EmptyPostgres_ReportsNameAndHost()
    {
        var errors = NewEditor().Validate();

        Assert.Equal(["Name is required.", "Host is required."], errors);
    }

    [Fact]
    public void Validate_Sqlite_RequiresFilePathNotHost()
    {
        var editor = NewEditor();
        editor.Name = "Local";
        editor.Dialect = DatabaseDialect.Sqlite;

        Assert.Equal(["File path is required."], editor.Validate());

        editor.FilePath = "a.db";
        Assert.Empty(editor.Validate());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    public void Validate_PortOutOfRange_Reports(int port)
    {
        var editor = NewEditor();
        editor.Name = "x";
        editor.Host = "h";
        editor.Port = port;

        Assert.Equal(["Port must be between 1 and 65535."], editor.Validate());
    }

    [Fact]
    public void Validate_SshEnabled_ChecksSshFields()
    {
        var editor = NewEditor();
        editor.Name = "x";
        editor.Host = "h";
        editor.UseSsh = true;
        editor.SshAuthMethod = SshAuthMethod.PrivateKey;

        Assert.Equal(
            ["SSH host is required.", "SSH user name is required.", "SSH private key path is required."],
            editor.Validate());
    }

    [Fact]
    public void SaveCommand_Invalid_IsDisabledUntilValid()
    {
        var editor = NewEditor();
        Assert.False(editor.SaveCommand.CanExecute(null));

        editor.Name = "x";
        editor.Host = "h";

        Assert.True(editor.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task Save_NewProfile_SavesWithPassword()
    {
        var editor = NewEditor();
        editor.Name = "x";
        editor.Host = "h";
        editor.Password = "pw";

        await editor.SaveCommand.ExecuteAsync(null);

        var saved = Assert.Single(_store.Saved);
        Assert.Equal("h", saved.Profile.Host);
        Assert.Equal("pw", saved.Password);
    }

    [Fact]
    public async Task Save_PasswordUnchanged_PassesNull()
    {
        var existing = Existing();
        _store.Password = "secret";
        _store.SshSecret = "ssh";
        var editor = NewEditor(existing);
        await editor.LoadSecretsAsync(CancellationToken.None);
        Assert.Equal("secret", editor.Password);

        await editor.SaveCommand.ExecuteAsync(null);

        var saved = Assert.Single(_store.Saved);
        Assert.Equal(existing.Id, saved.Profile.Id);
        Assert.Null(saved.Password);
        Assert.Null(saved.SshSecret);
    }

    [Fact]
    public async Task Save_PasswordChanged_PassesNewValue()
    {
        _store.Password = "secret";
        var editor = NewEditor(Existing());
        await editor.LoadSecretsAsync(CancellationToken.None);
        editor.Password = "other";

        await editor.SaveCommand.ExecuteAsync(null);

        Assert.Equal("other", Assert.Single(_store.Saved).Password);
    }

    [Fact]
    public async Task Test_Success_ReportsServerVersion()
    {
        var editor = NewEditor();
        editor.Name = "x";
        editor.Host = "h";

        await editor.TestCommand.ExecuteAsync(null);

        Assert.Contains("16.1", editor.TestStatus);
        Assert.Empty(_dialogs.Errors);
        Assert.True(_provider.Connection.Disposed);
    }

    [Fact]
    public async Task Test_Failure_ShowsError()
    {
        _provider.Failure = new InvalidOperationException("boom");
        var editor = NewEditor();

        await editor.TestCommand.ExecuteAsync(null);

        Assert.Equal("boom", Assert.Single(_dialogs.Errors).Message);
    }

    [Fact]
    public async Task Delete_Confirmed_RemovesProfile()
    {
        var profile = Existing();
        _store.Profiles.Add(profile);
        var list = new ConnectionListViewModel(_store, _registry, _dialogs);
        await list.LoadCommand.ExecuteAsync(null);
        list.SelectedProfile = profile;

        await list.DeleteCommand.ExecuteAsync(null);

        Assert.Empty(list.Profiles);
        Assert.Equal([profile.Id], _store.Deleted);
    }

    [Fact]
    public async Task Delete_Declined_KeepsProfile()
    {
        var profile = Existing();
        _store.Profiles.Add(profile);
        _dialogs.ConfirmResult = false;
        var list = new ConnectionListViewModel(_store, _registry, _dialogs);
        await list.LoadCommand.ExecuteAsync(null);
        list.SelectedProfile = profile;

        await list.DeleteCommand.ExecuteAsync(null);

        Assert.Single(list.Profiles);
        Assert.Empty(_store.Deleted);
    }

    [Fact]
    public async Task EditAndNew_RaiseEditRequested()
    {
        var profile = Existing();
        _store.Profiles.Add(profile);
        var list = new ConnectionListViewModel(_store, _registry, _dialogs);
        var editors = new List<ConnectionEditorViewModel>();
        list.EditRequested += (_, e) => editors.Add(e);
        await list.LoadCommand.ExecuteAsync(null);
        Assert.False(list.EditCommand.CanExecute(null));
        list.SelectedProfile = profile;

        await list.EditCommand.ExecuteAsync(null);
        await list.NewCommand.ExecuteAsync(null);

        Assert.Equal(["Prod", ""], editors.Select(e => e.Name));
    }

    private sealed class FakeStore : IProfileStore
    {
        public List<ConnectionProfile> Profiles { get; } = [];
        public List<(ConnectionProfile Profile, string? Password, string? SshSecret)> Saved { get; } = [];
        public List<Guid> Deleted { get; } = [];
        public string? Password { get; set; }
        public string? SshSecret { get; set; }

        public ValueTask<IReadOnlyList<ConnectionProfile>> ListAsync(CancellationToken ct) =>
            ValueTask.FromResult<IReadOnlyList<ConnectionProfile>>([.. Profiles]);

        public ValueTask SaveAsync(ConnectionProfile profile, string? password, string? sshSecret, CancellationToken ct)
        {
            Saved.Add((profile, password, sshSecret));
            return ValueTask.CompletedTask;
        }

        public ValueTask DeleteAsync(Guid id, CancellationToken ct)
        {
            Deleted.Add(id);
            return ValueTask.CompletedTask;
        }

        public ValueTask<string?> GetPasswordAsync(Guid id, CancellationToken ct) => ValueTask.FromResult(Password);

        public ValueTask<string?> GetSshSecretAsync(Guid id, CancellationToken ct) => ValueTask.FromResult(SshSecret);
    }

    private sealed class FakeProvider : IDatabaseProvider
    {
        public DatabaseDialect Dialect => DatabaseDialect.PostgreSql;
        public FakeConnection Connection { get; } = new();
        public Exception? Failure { get; set; }

        public ValueTask<IDatabaseConnection> OpenAsync(ConnectionProfile profile, string? password, CancellationToken cancellationToken) =>
            Failure is { } failure ? throw failure : ValueTask.FromResult<IDatabaseConnection>(Connection);
    }

    private sealed class FakeConnection : IDatabaseConnection
    {
        public bool Disposed { get; private set; }
        public string ServerVersion => "16.1";

        public ValueTask<IResultReader> ExecuteAsync(string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask<SchemaSnapshot> ReadSchemaAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
