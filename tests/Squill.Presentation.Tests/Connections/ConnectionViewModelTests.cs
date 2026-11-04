using Squill.Core;
using Squill.Data;
using Squill.Platform.Profiles;
using Squill.Presentation.Connections;
using Squill.Presentation.Localization;
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

        Assert.Equal([UiText.Get("connections.errName"), UiText.Get("connections.errHost")], errors);
    }

    [Fact]
    public void Validate_Messages_AreFrench()
    {
        Assert.Equal(["Le nom est obligatoire.", "L'hôte est obligatoire."], NewEditor().Validate());
    }

    [Theory]
    [InlineData(DatabaseDialect.PostgreSql, true, false, true)]
    [InlineData(DatabaseDialect.MySql, true, false, true)]
    [InlineData(DatabaseDialect.SqlServer, true, true, true)]
    [InlineData(DatabaseDialect.Sqlite, false, false, false)]
    public void Dialect_SetsSupportedOptions(DatabaseDialect dialect, bool tls, bool trust, bool timeouts)
    {
        var editor = NewEditor();
        editor.Dialect = dialect;

        Assert.Equal(tls, editor.SupportsTls);
        Assert.Equal(trust, editor.SupportsTrustCertificate);
        Assert.Equal(timeouts, editor.SupportsTimeouts);
    }

    [Fact]
    public async Task Save_Options_RoundTripThroughProfile()
    {
        var editor = NewEditor();
        editor.Name = "x";
        editor.Dialect = DatabaseDialect.SqlServer;
        editor.Host = "h";
        editor.Tls = TlsMode.VerifyFull;
        editor.TrustServerCertificate = true;
        editor.ConnectTimeoutText = "30";
        editor.CommandTimeoutText = "120";

        await editor.SaveCommand.ExecuteAsync(null);

        var saved = Assert.Single(_store.Saved).Profile;
        Assert.Equal((TlsMode.VerifyFull, true, 30, 120), (saved.Tls, saved.TrustServerCertificate, saved.ConnectTimeoutSeconds, saved.CommandTimeoutSeconds));
        var reopened = NewEditor(saved);
        Assert.Equal((TlsMode.VerifyFull, true, "30", "120"), (reopened.Tls, reopened.TrustServerCertificate, reopened.ConnectTimeoutText, reopened.CommandTimeoutText));
    }

    [Fact]
    public async Task Save_Sqlite_DropsServerOnlyOptions()
    {
        var editor = NewEditor();
        editor.Name = "x";
        editor.Dialect = DatabaseDialect.Sqlite;
        editor.FilePath = "a.db";
        editor.Tls = TlsMode.Require;
        editor.TrustServerCertificate = true;
        editor.ConnectTimeoutText = "99";
        editor.CommandTimeoutText = "7";

        await editor.SaveCommand.ExecuteAsync(null);

        var saved = Assert.Single(_store.Saved).Profile;
        Assert.Equal((TlsMode.Default, false, 15, 7), (saved.Tls, saved.TrustServerCertificate, saved.ConnectTimeoutSeconds, saved.CommandTimeoutSeconds));
    }

    [Fact]
    public void Validate_NegativeTimeout_ReportsRange()
    {
        var editor = NewEditor();
        editor.Name = "x";
        editor.Host = "h";
        editor.ConnectTimeoutText = "-1";

        Assert.Equal([IssueText.Format(CoreMessageCodes.ConnectionConnectTimeoutRange, ["86400"], "")], editor.Validate());
    }

    [Fact]
    public void Validate_NonNumericTimeout_Reports()
    {
        var editor = NewEditor();
        editor.Name = "x";
        editor.Host = "h";
        editor.CommandTimeoutText = "abc";

        Assert.Equal([UiText.Get("connections.errCommandTimeout")], editor.Validate());
    }

    [Fact]
    public void Validate_Sqlite_IgnoresConnectTimeout()
    {
        var editor = NewEditor();
        editor.Name = "x";
        editor.Dialect = DatabaseDialect.Sqlite;
        editor.FilePath = "a.db";
        editor.ConnectTimeoutText = "abc";

        Assert.Empty(editor.Validate());
    }

    [Fact]
    public void Validate_Sqlite_RequiresFilePathNotHost()
    {
        var editor = NewEditor();
        editor.Name = "Local";
        editor.Dialect = DatabaseDialect.Sqlite;

        Assert.Equal([UiText.Get("connections.errFile")], editor.Validate());

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

        Assert.Equal([UiText.Get("connections.errPort")], editor.Validate());
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
            [UiText.Get("connections.errSshHost"), UiText.Get("connections.errSshUser"), UiText.Get("connections.errSshKey")],
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
    public async Task Save_EnvironmentAndFolder_AreStoredTrimmedAndBlankFolderIsNull()
    {
        var editor = NewEditor();
        editor.Name = "x";
        editor.Host = "h";
        editor.Environment = ConnectionEnvironment.Staging;
        editor.Folder = "  Team  ";
        await editor.SaveCommand.ExecuteAsync(null);
        editor.Folder = "   ";
        await editor.SaveCommand.ExecuteAsync(null);

        Assert.Equal(ConnectionEnvironment.Staging, _store.Saved[0].Profile.Environment);
        Assert.Equal("Team", _store.Saved[0].Profile.Folder);
        Assert.Null(_store.Saved[1].Profile.Folder);
    }

    [Fact]
    public void Validate_FolderLongerThan64_ReportsError()
    {
        var editor = NewEditor(Existing());
        editor.Folder = new string('f', 65);

        Assert.Contains(UiText.Format("connections.errFolder", 64), editor.Validate());
        editor.Folder = new string('f', 64);
        Assert.Empty(editor.Validate());
    }

    [Fact]
    public async Task Groups_OrderedByFolderThenName_UngroupedLast()
    {
        _store.Profiles.Add(Existing() with { Name = "b", Folder = "Zed" });
        _store.Profiles.Add(Existing() with { Name = "z" });
        _store.Profiles.Add(Existing() with { Name = "a", Folder = "Zed", Environment = ConnectionEnvironment.Local });
        _store.Profiles.Add(Existing() with { Name = "c", Folder = "Alpha" });
        _store.Profiles.Add(Existing() with { Name = "y" });
        var list = new ConnectionListViewModel(_store, _registry, _dialogs);
        await list.LoadCommand.ExecuteAsync(null);

        Assert.Equal(["Alpha", "Zed", null], list.Groups.Select(g => g.Folder));
        Assert.Equal(["a", "b"], list.Groups[1].Items.Select(p => p.Name));
        Assert.Equal(ConnectionEnvironment.Local, list.Groups[1].Items[0].Environment);
        Assert.Equal(["y", "z"], list.Groups[2].Items.Select(p => p.Name));
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

    [Theory]
    [InlineData(UiLanguage.French, "Le nom est obligatoire.", "Fichier", "Le dossier ne doit pas dépasser 64 caractères.")]
    [InlineData(UiLanguage.English, "The name is required.", "file", "The folder must not exceed 64 characters.")]
    public void Validate_InvalidProfile_IsInTheChosenLanguage(UiLanguage language, string nameError, string fileWord, string folderError)
    {
        using var _ = UiText.Override(language);
        var editor = NewEditor();
        editor.Dialect = DatabaseDialect.Sqlite;
        editor.Folder = new string('f', 65);

        var errors = editor.Validate();

        Assert.Equal(3, errors.Count);
        Assert.Equal(nameError, errors[0]);
        Assert.Contains(fileWord, errors[1], StringComparison.OrdinalIgnoreCase);
        Assert.Equal(folderError, errors[2]);
    }

    [Theory]
    [InlineData(UiLanguage.French, "Connecté. Version du serveur : 16.1", "Connexion impossible : boom", "Échec du test de connexion")]
    [InlineData(UiLanguage.English, "Connected. Server version: 16.1", "Connection failed: boom", "Connection test failed")]
    public async Task Test_Outcome_IsTranslatedAndDriverMessageIsVerbatim(UiLanguage language, string ok, string failed, string title)
    {
        using var _ = UiText.Override(language);
        var editor = NewEditor();
        editor.Name = "x";
        editor.Host = "h";

        await editor.TestCommand.ExecuteAsync(null);
        Assert.Equal(ok, editor.TestStatus);

        _provider.Failure = new InvalidOperationException("boom");
        await editor.TestCommand.ExecuteAsync(null);

        Assert.Equal(failed, editor.TestStatus);
        Assert.Equal((title, "boom"), Assert.Single(_dialogs.Errors));
    }

    [Theory]
    [InlineData(UiLanguage.French, "Supprimer la connexion", "Supprimer la connexion 'Prod' ?")]
    [InlineData(UiLanguage.English, "Delete connection", "Delete the connection 'Prod'?")]
    public async Task Delete_Confirmation_IsInTheChosenLanguage(UiLanguage language, string title, string message)
    {
        using var _ = UiText.Override(language);
        var profile = Existing();
        _store.Profiles.Add(profile);
        var list = new ConnectionListViewModel(_store, _registry, _dialogs);
        await list.LoadCommand.ExecuteAsync(null);
        list.SelectedProfile = profile;

        await list.DeleteCommand.ExecuteAsync(null);

        Assert.Equal((title, message), Assert.Single(_dialogs.Confirms));
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

        public ValueTask<IReadOnlyList<string>> ListDatabasesAsync(CancellationToken cancellationToken) => ValueTask.FromResult<IReadOnlyList<string>>([]);

        public ValueTask<SchemaSnapshot> ReadSchemaAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
