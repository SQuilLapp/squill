using Squill.Core;
using Squill.Data;
using Squill.Platform.Profiles;
using Squill.Platform.Proxy;
using Squill.Platform.Secrets;
using Squill.Platform.Ssh;
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
    private readonly FakeProxies _proxies = new();
    private readonly FakeRunner _runner = new();

    public ConnectionViewModelTests() => _registry.Register(_provider);

    private ConnectionEditorViewModel NewEditor(ConnectionProfile? existing = null) => new(existing, _store, _registry, _dialogs, _proxies, _runner);

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
        editor.Dialect = DatabaseDialect.Informix;
        Assert.Equal(9089, editor.Port);
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
    public async Task Save_MariaDb_RoundTripsAndIsClearedWhenTheEngineChanges()
    {
        var editor = NewEditor();
        editor.Name = "x";
        editor.Dialect = DatabaseDialect.MySql;
        editor.MariaDb = true;
        editor.Host = "h";
        await editor.SaveCommand.ExecuteAsync(null);

        var saved = Assert.Single(_store.Saved).Profile;
        Assert.True(saved.MariaDb);
        var reopened = NewEditor(saved);
        Assert.True(reopened.MariaDb);
        reopened.Dialect = DatabaseDialect.PostgreSql;
        Assert.False(reopened.MariaDb);
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

    [Theory]
    [InlineData(DatabaseDialect.PostgreSql, true, true, true, true)]
    [InlineData(DatabaseDialect.MySql, true, true, false, false)]
    [InlineData(DatabaseDialect.SqlServer, true, false, true, false)]
    [InlineData(DatabaseDialect.Sqlite, false, false, false, false)]
    [InlineData(DatabaseDialect.Oracle, false, false, false, false)]
    public void Dialect_SetsTlsFileRules(DatabaseDialect dialect, bool ca, bool client, bool serverName, bool passphrase)
    {
        var editor = NewEditor();
        editor.Dialect = dialect;

        Assert.Equal(ca, editor.SupportsTlsFiles);
        Assert.Equal(client, editor.SupportsTlsClientFiles);
        Assert.Equal(serverName, editor.SupportsTlsServerName);
        Assert.Equal(passphrase, editor.SupportsTlsKeyPassphrase);
        Assert.Equal(client, editor.TlsClientFilesDisabledReason is null);
    }

    [Fact]
    public void Validate_TlsFiles_ReportsMissingFileAndKeyWithoutCertificate()
    {
        var editor = NewEditor(Existing());
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".pem");
        editor.TlsCaFile = missing;
        Assert.Contains(UiText.Format("issue.connection.tlsFileMissing", missing), editor.Validate());

        editor.TlsCaFile = "";
        var key = Path.GetTempFileName();
        try
        {
            editor.TlsClientKeyFile = key;
            Assert.Contains(UiText.Get("issue.connection.tlsKeyWithoutCertificate"), editor.Validate());
            editor.Dialect = DatabaseDialect.SqlServer; // client files are dropped for SQL Server, so the leftover cannot block the save
            Assert.DoesNotContain(UiText.Get("issue.connection.tlsKeyWithoutCertificate"), editor.Validate());
        }
        finally
        {
            File.Delete(key);
        }
    }

    [Fact]
    public async Task BrowseTls_FillsThePickedPath()
    {
        var editor = NewEditor(Existing());
        _dialogs.OpenFileResult = "C:/pki/ca.pem";

        await editor.BrowseTlsCaCommand.ExecuteAsync(null);
        await editor.BrowseTlsClientCertCommand.ExecuteAsync(null);
        await editor.BrowseTlsClientKeyCommand.ExecuteAsync(null);

        Assert.Equal("C:/pki/ca.pem", editor.TlsCaFile);
        Assert.Equal("C:/pki/ca.pem", editor.TlsClientCertFile);
        Assert.Equal("C:/pki/ca.pem", editor.TlsClientKeyFile);

        _dialogs.OpenFileResult = null;
        editor.TlsCaFile = "kept";
        await editor.BrowseTlsCaCommand.ExecuteAsync(null);
        Assert.Equal("kept", editor.TlsCaFile);
    }

    [Fact]
    public async Task Save_TlsFiles_AreSavedAndPassphraseGoesToTheSecretStore()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var ca = Path.Combine(dir.FullName, "ca.pem");
            var cert = Path.Combine(dir.FullName, "c.crt");
            var key = Path.Combine(dir.FullName, "c.key");
            foreach (var file in new[] { ca, cert, key })
                File.WriteAllText(file, "x");
            var editor = NewEditor(Existing());
            editor.TlsCaFile = ca;
            editor.TlsClientCertFile = cert;
            editor.TlsClientKeyFile = key;
            editor.TlsServerName = " db.example ";
            editor.TlsKeyPassphrase = "pw";

            await editor.SaveCommand.ExecuteAsync(null);

            var profile = Assert.Single(_store.Saved).Profile;
            Assert.Equal((ca, cert, key, "db.example"), (profile.TlsCaFile, profile.TlsClientCertFile, profile.TlsClientKeyFile, profile.TlsServerName));
            Assert.Null(profile.TlsKeyPassphrase);
            Assert.Equal(["pw"], _store.SavedPassphrases);
        }
        finally
        {
            dir.Delete(true);
        }
    }

    [Fact]
    public async Task Save_TlsPassphraseUnchangedOrCleared_KeepsOrDeletesTheSecret()
    {
        _store.TlsPassphrase = "old";
        var editor = NewEditor(Existing());
        await editor.LoadSecretsAsync(CancellationToken.None);
        Assert.Equal("old", editor.TlsKeyPassphrase);

        await editor.SaveCommand.ExecuteAsync(null);
        editor.TlsKeyPassphrase = "";
        await editor.SaveCommand.ExecuteAsync(null);

        Assert.Equal([null, ""], _store.SavedPassphrases);
    }

    [Fact]
    public async Task Test_PassesThePassphraseToTheProvider()
    {
        var editor = NewEditor(Existing());
        editor.TlsKeyPassphrase = "pw";

        await editor.TestCommand.ExecuteAsync(null);

        Assert.Equal("pw", _provider.LastProfile?.TlsKeyPassphrase);
    }

    [Fact]
    public void Proxy_KindChange_SwitchesTheDefaultPort_AndSqliteHasNone()
    {
        var editor = NewEditor(Existing());

        Assert.False(editor.UseProxy);
        editor.ProxyKindIndex = 1;
        Assert.Equal(1080, editor.ProxyPort);
        editor.ProxyKindIndex = 2;
        Assert.Equal(3128, editor.ProxyPort);
        editor.ProxyPort = 8080;
        editor.ProxyKindIndex = 1;
        Assert.Equal(8080, editor.ProxyPort);
        Assert.True(editor.UseProxy);
        editor.Dialect = DatabaseDialect.Sqlite;
        Assert.False(editor.SupportsProxy);
        Assert.False(editor.UseProxy);
    }

    [Fact]
    public void Validate_Proxy_NeedsHostAndPort_OnlyWhenSelected()
    {
        var editor = NewEditor(Existing());
        Assert.Empty(editor.Validate());

        editor.ProxyKindIndex = 1;
        editor.ProxyPort = 70000;

        Assert.Equal([UiText.Get("connections.errProxyHost"), UiText.Get("connections.errProxyPort")], editor.Validate());
        editor.ProxyHost = "proxy";
        editor.ProxyPort = 1080;
        Assert.Empty(editor.Validate());
    }

    [Fact]
    public async Task Save_Proxy_KeepsThePasswordOutOfTheProfile()
    {
        var editor = NewEditor(Existing());
        editor.ProxyKindIndex = 2;
        editor.ProxyHost = " proxy ";
        editor.ProxyUserName = "bob";
        editor.ProxyPassword = "s3cret";

        await editor.SaveCommand.ExecuteAsync(null);

        var profile = Assert.Single(_store.Saved).Profile;
        Assert.Equal(new ProxySettings(ProxyKind.HttpConnect, "proxy", 3128, "bob"), profile.Proxy);
        Assert.Equal(["s3cret"], _store.SavedProxySecrets);
    }

    [Fact]
    public async Task Save_ProxyPasswordUnchangedOrProxyRemoved_KeepsOrDeletesTheSecret()
    {
        _store.ProxySecret = "old";
        var existing = Existing() with { Proxy = new ProxySettings(ProxyKind.Socks5, "p", 1080, "bob") };
        var editor = NewEditor(existing);
        await editor.LoadSecretsAsync(CancellationToken.None);
        Assert.Equal((1, "old"), (editor.ProxyKindIndex, editor.ProxyPassword));

        await editor.SaveCommand.ExecuteAsync(null);
        editor.ProxyKindIndex = 0;
        await editor.SaveCommand.ExecuteAsync(null);

        Assert.Equal([null, ""], _store.SavedProxySecrets);
        Assert.Null(_store.Saved[1].Profile.Proxy);
    }

    [Fact]
    public async Task Test_GoesThroughTheProxy_AndFailureNeverShowsThePassword()
    {
        var editor = NewEditor(Existing());
        editor.ProxyKindIndex = 1;
        editor.ProxyHost = "proxy";
        editor.ProxyUserName = "bob";
        editor.ProxyPassword = "s3cret";

        await editor.TestCommand.ExecuteAsync(null);

        Assert.Equal(("db", 5432, "s3cret"), (_proxies.RemoteHost, _proxies.RemotePort, _proxies.Secret));
        Assert.Equal(("127.0.0.1", 4242, "db"), (_provider.LastProfile?.Host, _provider.LastProfile?.Port, _provider.LastProfile?.TlsServerName));
        Assert.True(_proxies.Disposed);
        Assert.True(editor.TestSucceeded);

        _proxies.Failure = new IOException("proxy refused");
        await editor.TestCommand.ExecuteAsync(null);
        Assert.False(editor.TestSucceeded);
        Assert.DoesNotContain("s3cret", editor.TestStatus);
    }

    [Fact]
    public void PasswordSource_FollowsTheCombo_AndSqliteIsAlwaysStored()
    {
        var editor = NewEditor();
        Assert.True(editor.PasswordIsStored);
        editor.PasswordSourceIndex = 1;
        Assert.Equal((PasswordSource.Ask, false, false), (editor.EffectivePasswordSource, editor.PasswordIsStored, editor.PasswordIsCommand));
        editor.PasswordSourceIndex = 2;
        Assert.True(editor.PasswordIsCommand);
        editor.Dialect = DatabaseDialect.Sqlite;
        Assert.Equal((PasswordSource.Stored, true, false), (editor.EffectivePasswordSource, editor.PasswordIsStored, editor.PasswordIsCommand));
    }

    [Fact]
    public void Validate_PasswordCommand_IsRequiredAndWellQuoted_OnlyForTheCommandSource()
    {
        var editor = NewEditor(Existing());
        editor.PasswordCommand = "\"unclosed";
        Assert.Empty(editor.Validate());
        editor.PasswordSourceIndex = 2;
        Assert.Equal([UiText.Get("connections.errPasswordCommand")], editor.Validate());
        editor.PasswordCommand = "   ";
        Assert.Single(editor.Validate());
        editor.PasswordCommand = "op read \"op://v/i\"";
        Assert.Empty(editor.Validate());
    }

    [Fact]
    public async Task Save_AskOrCommand_KeepsNoPassword_AndStoresTheCommandInTheProfile()
    {
        _store.Password = "old";
        var editor = NewEditor(Existing());
        await editor.LoadSecretsAsync(CancellationToken.None);
        editor.PasswordSourceIndex = 2;
        editor.PasswordCommand = "  op read x  ";
        await editor.SaveCommand.ExecuteAsync(null);
        editor.PasswordSourceIndex = 1;
        await editor.SaveCommand.ExecuteAsync(null);
        editor.PasswordSourceIndex = 0;
        await editor.SaveCommand.ExecuteAsync(null);

        Assert.Equal((PasswordSource.Command, "op read x"), (_store.Saved[0].Profile.PasswordSource, _store.Saved[0].Profile.PasswordCommand));
        Assert.Equal((PasswordSource.Ask, (string?)null), (_store.Saved[1].Profile.PasswordSource, _store.Saved[1].Profile.PasswordCommand));
        Assert.Equal(["", "", null], _store.Saved.Select(s => s.Password));
        Assert.Equal(PasswordSource.Stored, _store.Saved[2].Profile.PasswordSource);
    }

    [Fact]
    public void Existing_ProfileLoadsItsPasswordSource()
    {
        var editor = NewEditor(Existing() with { PasswordSource = PasswordSource.Command, PasswordCommand = "pass show db" });

        Assert.Equal((2, "pass show db"), (editor.PasswordSourceIndex, editor.PasswordCommand));
    }

    [Fact]
    public async Task TestPasswordCommand_ConfirmsShowingTheExactCommand_ThenReportsWithoutShowingTheOutput()
    {
        _runner.Output = "hunter2";
        var editor = NewEditor(Existing());
        editor.PasswordSourceIndex = 2;
        editor.PasswordCommand = "op read \"op://v/i\"";

        await editor.TestPasswordCommandCommand.ExecuteAsync(null);

        var confirm = Assert.Single(_dialogs.Confirms);
        Assert.Contains("op read \"op://v/i\"", confirm.Message);
        Assert.Equal("op read \"op://v/i\"", _runner.Line);
        Assert.True(editor.TestSucceeded);
        Assert.Equal(UiText.Get("connections.passwordCommandOk"), editor.TestHeadline);
        Assert.DoesNotContain("hunter2", editor.TestStatus);
    }

    [Fact]
    public async Task TestPasswordCommand_DeclinedRunsNothing_FailureNeverShowsTheOutput()
    {
        var editor = NewEditor(Existing());
        editor.PasswordSourceIndex = 2;
        editor.PasswordCommand = "tool";
        _dialogs.ConfirmResult = false;
        await editor.TestPasswordCommandCommand.ExecuteAsync(null);
        Assert.Null(_runner.Line);
        Assert.Null(editor.TestSucceeded);

        _dialogs.ConfirmResult = true;
        _runner.Failure = new PasswordCommandException(PasswordCommandFailure.ExitCode, "3", "denied");
        _runner.Output = "hunter2";
        await editor.TestPasswordCommandCommand.ExecuteAsync(null);
        Assert.False(editor.TestSucceeded);
        Assert.Contains("denied", Assert.Single(_dialogs.Errors).Message);
        Assert.DoesNotContain("hunter2", editor.TestStatus);
    }

    [Fact]
    public async Task Test_WithAsk_UsesThePromptedPassword_AndCancelStopsTheTest()
    {
        var editor = NewEditor(Existing());
        editor.PasswordSourceIndex = 1;
        _dialogs.PasswordResult = "typed";

        await editor.TestCommand.ExecuteAsync(null);

        Assert.Equal(("typed", "Prod", true), (_provider.LastPassword, Assert.Single(_dialogs.PasswordPrompts), editor.TestSucceeded));

        // Cancelling the prompt is not an error: nothing is tried and the result bar does not change.
        _dialogs.PasswordResult = null;
        _provider.LastPassword = "untouched";
        await editor.TestCommand.ExecuteAsync(null);
        Assert.Equal("untouched", _provider.LastPassword);
        Assert.Equal(2, _dialogs.PasswordPrompts.Count);
    }

    [Fact]
    public async Task Test_WithCommand_ConnectsWithTheCommandOutput()
    {
        _runner.Output = "from-cmd";
        var editor = NewEditor(Existing());
        editor.PasswordSourceIndex = 2;
        editor.PasswordCommand = "tool";

        await editor.TestCommand.ExecuteAsync(null);

        Assert.Equal("from-cmd", _provider.LastPassword);
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

        public string? TlsPassphrase { get; set; }
        public List<string?> SavedPassphrases { get; } = [];

        public ValueTask<string?> GetTlsKeyPassphraseAsync(Guid id, CancellationToken ct) => ValueTask.FromResult(TlsPassphrase);

        public string? ProxySecret { get; set; }
        public List<string?> SavedProxySecrets { get; } = [];

        public ValueTask<string?> GetProxySecretAsync(Guid id, CancellationToken ct) => ValueTask.FromResult(ProxySecret);

        public ValueTask SaveProxySecretAsync(Guid id, string? password, CancellationToken ct)
        {
            SavedProxySecrets.Add(password);
            return ValueTask.CompletedTask;
        }

        public ValueTask SaveTlsKeyPassphraseAsync(Guid id, string? passphrase, CancellationToken ct)
        {
            SavedPassphrases.Add(passphrase);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeProxies : IProxyTunnelFactory
    {
        public string? RemoteHost { get; private set; }
        public int RemotePort { get; private set; }
        public string? Secret { get; private set; }
        public bool Disposed { get; private set; }
        public Exception? Failure { get; set; }

        public ValueTask<ISshTunnel> OpenAsync(ProxySettings proxy, string? secret, string remoteHost, int remotePort, CancellationToken ct)
        {
            (RemoteHost, RemotePort, Secret) = (remoteHost, remotePort, secret);
            return Failure is { } failure ? throw failure : ValueTask.FromResult<ISshTunnel>(new FakeTunnel(this));
        }

        private sealed class FakeTunnel(FakeProxies owner) : ISshTunnel
        {
            public int LocalPort => 4242;

            public ValueTask DisposeAsync()
            {
                owner.Disposed = true;
                return ValueTask.CompletedTask;
            }
        }
    }

    private sealed class FakeRunner : IPasswordCommandRunner
    {
        public string Output { get; set; } = "pw";
        public string? Line { get; private set; }
        public Exception? Failure { get; set; }

        public ValueTask<string> RunAsync(string commandLine, CancellationToken ct)
        {
            Line = commandLine;
            return Failure is { } failure ? throw failure : ValueTask.FromResult(Output);
        }
    }

    private sealed class FakeProvider : IDatabaseProvider
    {
        public DatabaseDialect Dialect => DatabaseDialect.PostgreSql;
        public FakeConnection Connection { get; } = new();
        public Exception? Failure { get; set; }

        public ConnectionProfile? LastProfile { get; private set; }

        public string? LastPassword { get; set; }

        public ValueTask<IDatabaseConnection> OpenAsync(ConnectionProfile profile, string? password, CancellationToken cancellationToken)
        {
            LastProfile = profile;
            LastPassword = password;
            return Failure is { } failure ? throw failure : ValueTask.FromResult<IDatabaseConnection>(Connection);
        }
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
