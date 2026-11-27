using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Squill.Core;
using Squill.Data;
using Squill.Platform.Profiles;
using Squill.Platform.Proxy;
using Squill.Platform.Secrets;
using Squill.Presentation.Localization;

namespace Squill.Presentation.Connections;

/// <summary>Edits one connection profile: field values, validation, saving and a connection test.</summary>
public sealed partial class ConnectionEditorViewModel : ViewModelBase
{
    private readonly ConnectionProfile? _existing;
    private readonly IProfileStore _store;
    private readonly ProviderRegistry _providers;
    private readonly IDialogService _dialogs;
    private string? _loadedPassword;
    private string? _loadedSshSecret;
    private readonly IProxyTunnelFactory _proxies;
    private string? _loadedTlsPassphrase;
    private string? _loadedProxySecret;
    private readonly IPasswordCommandRunner _commandRunner;

    /// <summary>Creates an editor for a new profile, or for <paramref name="existing"/> when it is not <see langword="null"/>.</summary>
    /// <param name="existing">Profile to edit, or <see langword="null"/> to create one.</param>
    /// <param name="store">Where the profile is saved.</param>
    /// <param name="providers">Used by the connection test.</param>
    /// <param name="dialogs">Used to report test failures.</param>
    /// <param name="proxies">Opens the proxy forwarder of the connection test; <see langword="null"/> uses <see cref="ProxyTunnelFactory"/>.</param>
    /// <param name="commandRunner">Runs the password command of the connection test; <see langword="null"/> uses <see cref="PasswordCommandRunner"/>.</param>
    public ConnectionEditorViewModel(ConnectionProfile? existing, IProfileStore store, ProviderRegistry providers, IDialogService dialogs, IProxyTunnelFactory? proxies = null, IPasswordCommandRunner? commandRunner = null)
    {
        _commandRunner = commandRunner ?? new PasswordCommandRunner();
        _proxies = proxies ?? new ProxyTunnelFactory();
        _existing = existing;
        _store = store;
        _providers = providers;
        _dialogs = dialogs;
        if (existing is null)
        {
            Port = DefaultPort(Dialect);
            return;
        }

        Name = existing.Name;
        Dialect = existing.Dialect;
        MariaDb = existing.MariaDb;
        WalletPath = existing.WalletPath ?? "";
        Host = existing.Host ?? "";
        Port = existing.Port;
        Database = existing.Database ?? "";
        UserName = existing.UserName ?? "";
        FilePath = existing.FilePath ?? "";
        Environment = existing.Environment;
        Folder = existing.Folder ?? "";
        if (existing.Ssh is { } ssh)
        {
            UseSsh = true;
            SshHost = ssh.Host;
            SshPort = ssh.Port;
            SshUserName = ssh.UserName;
            SshAuthMethod = ssh.AuthMethod;
            SshPrivateKeyPath = ssh.PrivateKeyPath ?? "";
        }

        Tls = existing.Tls;
        TrustServerCertificate = existing.TrustServerCertificate;
        TlsCaFile = existing.TlsCaFile ?? "";
        TlsClientCertFile = existing.TlsClientCertFile ?? "";
        TlsClientKeyFile = existing.TlsClientKeyFile ?? "";
        TlsServerName = existing.TlsServerName ?? "";
        if (existing.Proxy is { } proxy)
        {
            ProxyKindIndex = proxy.Kind == ProxyKind.Socks5 ? 1 : 2;
            ProxyHost = proxy.Host;
            ProxyPort = proxy.Port;
            ProxyUserName = proxy.UserName ?? "";
        }

        PasswordSourceIndex = (int)existing.PasswordSource;
        PasswordCommand = existing.PasswordCommand ?? "";
        ConnectTimeoutText = existing.ConnectTimeoutSeconds.ToString(CultureInfo.InvariantCulture);
        CommandTimeoutText = existing.CommandTimeoutSeconds.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Raised after the profile has been saved.</summary>
    public event EventHandler? Saved;

    /// <summary>Gets or sets the display name of the profile.</summary>
    [ObservableProperty]
    public partial string Name { get; set; } = "";

    /// <summary>Gets or sets the database engine; changing it switches the default port.</summary>
    [ObservableProperty]
    public partial DatabaseDialect Dialect { get; set; } = DatabaseDialect.PostgreSql;

    /// <summary>Gets or sets whether a <see cref="DatabaseDialect.MySql"/> profile targets MariaDB: same driver and SQL, a separate logo and a hook for later differences. Cleared when the engine changes.</summary>
    [ObservableProperty]
    public partial bool MariaDb { get; set; }

    /// <summary>Gets or sets the Oracle wallet folder (the <c>TNS_ADMIN</c> directory holding <c>cwallet.sso</c>); blank for none. Only kept for Oracle profiles.</summary>
    [ObservableProperty]
    public partial string WalletPath { get; set; } = "";

    /// <summary>Gets or sets the server host name (unused for SQLite).</summary>
    [ObservableProperty]
    public partial string Host { get; set; } = "";

    /// <summary>Gets or sets the server port; <see langword="null"/> means the engine default.</summary>
    [ObservableProperty]
    public partial int? Port { get; set; }

    /// <summary>Gets or sets the database name.</summary>
    [ObservableProperty]
    public partial string Database { get; set; } = "";

    /// <summary>Gets or sets the account used to sign in.</summary>
    [ObservableProperty]
    public partial string UserName { get; set; } = "";

    /// <summary>Gets or sets the database password; saved as <see langword="null"/> (kept) while it equals the loaded value.</summary>
    [ObservableProperty]
    public partial string Password { get; set; } = "";

    /// <summary>Gets or sets where the password comes from as a combo index: 0 the system secret store, 1 asked at each connect, 2 an external command.</summary>
    [ObservableProperty]
    public partial int PasswordSourceIndex { get; set; }

    /// <summary>Gets or sets the command line that prints the password (run without a shell); it is stored in the profile, so it must not contain the password itself.</summary>
    [ObservableProperty]
    public partial string PasswordCommand { get; set; } = "";

    /// <summary>Gets the password source that applies to the selected engine (SQLite has no password, so it is always stored).</summary>
    public PasswordSource EffectivePasswordSource => Dialect != DatabaseDialect.Sqlite && PasswordSourceIndex is 1 or 2 ? (PasswordSource)PasswordSourceIndex : PasswordSource.Stored;

    /// <summary>Gets whether the password box is shown (source "stored").</summary>
    public bool PasswordIsStored => EffectivePasswordSource == PasswordSource.Stored;

    /// <summary>Gets whether the command box and its test button are shown (source "command").</summary>
    public bool PasswordIsCommand => EffectivePasswordSource == PasswordSource.Command;

    /// <summary>Gets or sets the SQLite database file path.</summary>
    [ObservableProperty]
    public partial string FilePath { get; set; } = "";

    /// <summary>Gets or sets the environment the connection points at.</summary>
    [ObservableProperty]
    public partial ConnectionEnvironment Environment { get; set; }

    /// <summary>Gets or sets the folder grouping the connection; blank means none, at most <see cref="MaxFolderLength"/> characters once trimmed.</summary>
    [ObservableProperty]
    public partial string Folder { get; set; } = "";

    /// <summary>Gets or sets whether the connection goes through an SSH tunnel.</summary>
    [ObservableProperty]
    public partial bool UseSsh { get; set; }

    /// <summary>Gets or sets the SSH server host.</summary>
    [ObservableProperty]
    public partial string SshHost { get; set; } = "";

    /// <summary>Gets or sets the SSH server port.</summary>
    [ObservableProperty]
    public partial int? SshPort { get; set; } = 22;

    /// <summary>Gets or sets the SSH account.</summary>
    [ObservableProperty]
    public partial string SshUserName { get; set; } = "";

    /// <summary>Gets or sets how the SSH account authenticates.</summary>
    [ObservableProperty]
    public partial SshAuthMethod SshAuthMethod { get; set; } = SshAuthMethod.Password;

    /// <summary>Gets or sets the private key file path.</summary>
    [ObservableProperty]
    public partial string SshPrivateKeyPath { get; set; } = "";

    /// <summary>Gets or sets the SSH password or key passphrase; saved as <see langword="null"/> (kept) while it equals the loaded value.</summary>
    [ObservableProperty]
    public partial string SshSecret { get; set; } = "";

    /// <summary>Gets or sets how the connection is secured (ignored for SQLite).</summary>
    [ObservableProperty]
    public partial TlsMode Tls { get; set; }

    /// <summary>Gets or sets whether the server certificate is accepted without validation (SQL Server only).</summary>
    [ObservableProperty]
    public partial bool TrustServerCertificate { get; set; }

    /// <summary>Gets or sets the path of the certificate authority file (PEM or CRT) that signs the server certificate; blank for the system roots.</summary>
    [ObservableProperty]
    public partial string TlsCaFile { get; set; } = "";

    /// <summary>Gets or sets the path of the client certificate file.</summary>
    [ObservableProperty]
    public partial string TlsClientCertFile { get; set; } = "";

    /// <summary>Gets or sets the path of the client private key file.</summary>
    [ObservableProperty]
    public partial string TlsClientKeyFile { get; set; } = "";

    /// <summary>Gets or sets the host name the server certificate is checked against; blank means the connection host.</summary>
    [ObservableProperty]
    public partial string TlsServerName { get; set; } = "";

    /// <summary>Gets or sets the passphrase of the client key; kept in the secret store, saved as <see langword="null"/> (kept) while it equals the loaded value.</summary>
    [ObservableProperty]
    public partial string TlsKeyPassphrase { get; set; } = "";

    /// <summary>Gets or sets the proxy kind as a combo index: 0 none, 1 SOCKS5, 2 HTTP CONNECT.</summary>
    [ObservableProperty]
    public partial int ProxyKindIndex { get; set; }

    /// <summary>Gets or sets the proxy host name or address.</summary>
    [ObservableProperty]
    public partial string ProxyHost { get; set; } = "";

    /// <summary>Gets or sets the proxy port.</summary>
    [ObservableProperty]
    public partial int? ProxyPort { get; set; } = DefaultProxyPort(1);

    /// <summary>Gets or sets the proxy account; blank for an open proxy.</summary>
    [ObservableProperty]
    public partial string ProxyUserName { get; set; } = "";

    /// <summary>Gets or sets the proxy password; kept in the secret store, saved as <see langword="null"/> (kept) while it equals the loaded value.</summary>
    [ObservableProperty]
    public partial string ProxyPassword { get; set; } = "";

    /// <summary>Gets whether a proxy is selected and applies to the selected engine.</summary>
    public bool UseProxy => SupportsProxy && ProxyKindIndex is 1 or 2;

    /// <summary>Gets whether a proxy applies to the selected engine (every engine except SQLite, which has no network).</summary>
    public bool SupportsProxy => Dialect != DatabaseDialect.Sqlite;

    /// <summary>Gets or sets the connect timeout in seconds as typed; 0 means no timeout.</summary>
    [ObservableProperty]
    public partial string ConnectTimeoutText { get; set; } = "15";

    /// <summary>Gets or sets the command timeout in seconds as typed; 0 means no timeout.</summary>
    [ObservableProperty]
    public partial string CommandTimeoutText { get; set; } = "0";

    /// <summary>Gets whether <see cref="Tls"/> applies to the selected engine.</summary>
    public bool SupportsTls => Dialect != DatabaseDialect.Sqlite;

    /// <summary>Gets whether <see cref="TrustServerCertificate"/> applies to the selected engine.</summary>
    public bool SupportsTrustCertificate => Dialect == DatabaseDialect.SqlServer;

    /// <summary>Gets whether the CA file applies to the selected engine (PostgreSQL, MySQL and SQL Server).</summary>
    public bool SupportsTlsFiles => Dialect is DatabaseDialect.PostgreSql or DatabaseDialect.MySql or DatabaseDialect.SqlServer;

    /// <summary>Gets whether the client certificate and key apply to the selected engine (PostgreSQL and MySQL).</summary>
    public bool SupportsTlsClientFiles => Dialect is DatabaseDialect.PostgreSql or DatabaseDialect.MySql;

    /// <summary>Gets whether the certificate server name applies to the selected engine (PostgreSQL and SQL Server; MySqlConnector has no override).</summary>
    public bool SupportsTlsServerName => Dialect is DatabaseDialect.PostgreSql or DatabaseDialect.SqlServer;

    /// <summary>Gets whether the key passphrase applies to the selected engine (PostgreSQL; MySqlConnector cannot read encrypted keys).</summary>
    public bool SupportsTlsKeyPassphrase => Dialect == DatabaseDialect.PostgreSql;

    /// <summary>Gets why the client certificate fields are disabled for the selected engine, or <see langword="null"/> when they are enabled.</summary>
    public string? TlsClientFilesDisabledReason => SupportsTlsClientFiles ? null : UiText.Get("connections.tlsClientUnsupported");

    /// <summary>Gets whether the connect timeout applies to the selected engine; the command timeout always does.</summary>
    public bool SupportsTimeouts => Dialect != DatabaseDialect.Sqlite;

    /// <summary>Gets the headline of the result bar: the success text of a connection test, or the status of the last test.</summary>
    public string TestHeadline => TestSucceeded == true && TestServerVersion is not null ? UiText.Get("connections.testSucceeded") : TestStatus ?? "";

    /// <summary>Gets the outcome of the last connection test, or <see langword="null"/> before any test.</summary>
    [ObservableProperty]
    public partial string? TestStatus { get; private set; }

    /// <summary>Gets whether the last connection test succeeded, or <see langword="null"/> before any test.</summary>
    [ObservableProperty]
    public partial bool? TestSucceeded { get; private set; }

    /// <summary>Gets the server version reported by the last successful test, or <see langword="null"/>.</summary>
    [ObservableProperty]
    public partial string? TestServerVersion { get; private set; }

    /// <summary>The longest allowed <see cref="Folder"/> name.</summary>
    public const int MaxFolderLength = 64;

    /// <summary>Gets the default port of an engine.</summary>
    /// <param name="dialect">The engine.</param>
    /// <returns>The port, or <see langword="null"/> for SQLite.</returns>
    public static int? DefaultPort(DatabaseDialect dialect) => dialect switch
    {
        DatabaseDialect.PostgreSql => 5432,
        DatabaseDialect.MySql => 3306,
        DatabaseDialect.SqlServer => 1433,
        DatabaseDialect.Oracle => 1521,
        DatabaseDialect.Informix => 9089, // the DRDA listener (drsoctcp), not the onsoctcp one on 9088
        _ => null,
    };

    /// <summary>Loads the stored password and SSH secret of the edited profile; a no-op for a new profile.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the secrets are loaded.</returns>
    public async Task LoadSecretsAsync(CancellationToken ct)
    {
        if (_existing is null)
            return;
        _loadedPassword = await _store.GetPasswordAsync(_existing.Id, ct);
        _loadedSshSecret = await _store.GetSshSecretAsync(_existing.Id, ct);
        _loadedTlsPassphrase = await _store.GetTlsKeyPassphraseAsync(_existing.Id, ct);
        TlsKeyPassphrase = _loadedTlsPassphrase ?? "";
        _loadedProxySecret = await _store.GetProxySecretAsync(_existing.Id, ct);
        ProxyPassword = _loadedProxySecret ?? "";
        Password = _loadedPassword ?? "";
        SshSecret = _loadedSshSecret ?? "";
    }

    /// <summary>Checks the current field values.</summary>
    /// <returns>One message per problem; empty when the profile can be saved.</returns>
    public IReadOnlyList<string> Validate()
    {
        List<string> errors = [];
        if (string.IsNullOrWhiteSpace(Name))
            errors.Add(UiText.Get("connections.errName"));
        if (Dialect == DatabaseDialect.Sqlite)
        {
            if (string.IsNullOrWhiteSpace(FilePath))
                errors.Add(UiText.Get("connections.errFile"));
        }
        else
        {
            if (string.IsNullOrWhiteSpace(Host))
                errors.Add(UiText.Get("connections.errHost"));
            if (Port is { } port and (< 1 or > 65535))
                errors.Add(UiText.Get("connections.errPort"));
        }

        if (Folder.Trim().Length > MaxFolderLength)
            errors.Add(UiText.Format("connections.errFolder", MaxFolderLength));

        AddTimeoutErrors(errors);
        AddTlsErrors(errors);

        if (PasswordIsCommand && !IsValidCommand(PasswordCommand))
            errors.Add(UiText.Get("connections.errPasswordCommand"));

        if (UseProxy)
        {
            if (string.IsNullOrWhiteSpace(ProxyHost))
                errors.Add(UiText.Get("connections.errProxyHost"));
            if (ProxyPort is not (>= 1 and <= 65535))
                errors.Add(UiText.Get("connections.errProxyPort"));
        }

        if (UseSsh)
        {
            if (string.IsNullOrWhiteSpace(SshHost))
                errors.Add(UiText.Get("connections.errSshHost"));
            if (SshPort is not (>= 1 and <= 65535))
                errors.Add(UiText.Get("connections.errSshPort"));
            if (string.IsNullOrWhiteSpace(SshUserName))
                errors.Add(UiText.Get("connections.errSshUser"));
            if (SshAuthMethod == SshAuthMethod.PrivateKey && string.IsNullOrWhiteSpace(SshPrivateKeyPath))
                errors.Add(UiText.Get("connections.errSshKey"));
        }

        return errors;
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        SaveCommand.NotifyCanExecuteChanged();
    }

    partial void OnDialectChanged(DatabaseDialect oldValue, DatabaseDialect newValue)
    {
        OnPropertyChanged(nameof(SupportsTls));
        OnPropertyChanged(nameof(SupportsTrustCertificate));
        OnPropertyChanged(nameof(SupportsTimeouts));
        OnPropertyChanged(nameof(SupportsTlsFiles));
        OnPropertyChanged(nameof(SupportsProxy));
        OnPropertyChanged(nameof(UseProxy));
        OnPropertyChanged(nameof(EffectivePasswordSource));
        OnPropertyChanged(nameof(PasswordIsStored));
        OnPropertyChanged(nameof(PasswordIsCommand));
        OnPropertyChanged(nameof(SupportsTlsClientFiles));
        OnPropertyChanged(nameof(SupportsTlsServerName));
        OnPropertyChanged(nameof(SupportsTlsKeyPassphrase));
        OnPropertyChanged(nameof(TlsClientFilesDisabledReason));
        MariaDb = false;
        if (Port is null || Port == DefaultPort(oldValue))
            Port = DefaultPort(newValue);
    }

    private bool CanSave() => Validate().Count == 0;

    private static string? NullIfBlank(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool TryParseTimeout(string text, out int seconds) =>
        int.TryParse(text.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out seconds);

    private void AddTimeoutErrors(List<string> errors)
    {
        var connectShown = SupportsTimeouts;
        var connectOk = TryParseTimeout(ConnectTimeoutText, out var connect);
        var commandOk = TryParseTimeout(CommandTimeoutText, out var command);
        if (connectShown && !connectOk)
            errors.Add(UiText.Get("connections.errConnectTimeout"));
        if (!commandOk)
            errors.Add(UiText.Get("connections.errCommandTimeout"));
        var checkedProfile = new ConnectionProfile(Guid.Empty, "", Dialect, null, null, null, null, null, null,
            ConnectTimeoutSeconds: connectShown && connectOk ? connect : 0, CommandTimeoutSeconds: commandOk ? command : 0);
        foreach (var issue in checkedProfile.ValidateOptions())
            errors.Add(IssueText.Format(issue.Code, issue.Args, issue.Message));
    }

    private void AddTlsErrors(List<string> errors)
    {
        // Only what BuildProfile keeps is checked, so a hidden leftover cannot block the save.
        foreach (var issue in BuildProfile(withSsh: false).ValidateOptions()
                     .Where(i => i.Code is CoreMessageCodes.ConnectionTlsFileMissing or CoreMessageCodes.ConnectionTlsKeyWithoutCertificate))
            errors.Add(IssueText.Format(issue.Code, issue.Args, issue.Message));
    }

    partial void OnPasswordSourceIndexChanged(int oldValue, int newValue)
    {
        OnPropertyChanged(nameof(EffectivePasswordSource));
        OnPropertyChanged(nameof(PasswordIsStored));
        OnPropertyChanged(nameof(PasswordIsCommand));
    }

    private static bool IsValidCommand(string commandLine)
    {
        try
        {
            PasswordCommandRunner.Split(commandLine);
            return true;
        }
        catch (PasswordCommandException)
        {
            return false;
        }
    }

    partial void OnProxyKindIndexChanged(int oldValue, int newValue)
    {
        OnPropertyChanged(nameof(UseProxy));
        if (newValue is 1 or 2 && (ProxyPort is null || ProxyPort == DefaultProxyPort(oldValue)))
            ProxyPort = DefaultProxyPort(newValue);
    }

    private static int DefaultProxyPort(int kindIndex) => kindIndex == 2 ? 3128 : 1080;

    [RelayCommand]
    private Task BrowseTlsCaAsync() => PickAsync("connections.pickTlsCa", ["pem", "crt", "cer"], path => TlsCaFile = path);

    [RelayCommand]
    private Task BrowseTlsClientCertAsync() => PickAsync("connections.pickTlsCert", ["pem", "crt", "cer"], path => TlsClientCertFile = path);

    [RelayCommand]
    private Task BrowseTlsClientKeyAsync() => PickAsync("connections.pickTlsKey", ["pem", "key"], path => TlsClientKeyFile = path);

    private async Task PickAsync(string titleKey, string[] extensions, Action<string> apply)
    {
        if (await _dialogs.PickOpenFileAsync(UiText.Get(titleKey), extensions) is { } path)
            apply(path);
    }

    private ConnectionProfile BuildProfile(bool withSsh)
    {
        var sqlite = Dialect == DatabaseDialect.Sqlite;
        SshTunnelSettings? ssh = withSsh && UseSsh
            ? new(SshHost.Trim(), SshPort ?? 22, SshUserName.Trim(), SshAuthMethod,
                SshAuthMethod == SshAuthMethod.PrivateKey ? SshPrivateKeyPath.Trim() : null)
            : null;
        return new ConnectionProfile(
            _existing?.Id ?? Guid.NewGuid(),
            Name.Trim(),
            Dialect,
            sqlite ? null : NullIfBlank(Host),
            sqlite ? null : Port,
            NullIfBlank(Database),
            sqlite ? null : NullIfBlank(UserName),
            sqlite ? NullIfBlank(FilePath) : null,
            ssh,
            Environment,
            NullIfBlank(Folder),
            sqlite ? TlsMode.Default : Tls,
            Dialect == DatabaseDialect.SqlServer && TrustServerCertificate,
            sqlite || !TryParseTimeout(ConnectTimeoutText, out var connect) ? _existing?.ConnectTimeoutSeconds ?? 15 : connect,
            TryParseTimeout(CommandTimeoutText, out var command) ? command : 0,
            Dialect == DatabaseDialect.MySql && MariaDb,
            Dialect == DatabaseDialect.Oracle ? NullIfBlank(WalletPath) : null,
            SupportsTlsFiles ? NullIfBlank(TlsCaFile) : null,
            SupportsTlsClientFiles ? NullIfBlank(TlsClientCertFile) : null,
            SupportsTlsClientFiles ? NullIfBlank(TlsClientKeyFile) : null,
            SupportsTlsServerName ? NullIfBlank(TlsServerName) : null,
            UseProxy ? new ProxySettings(ProxyKindIndex == 1 ? ProxyKind.Socks5 : ProxyKind.HttpConnect, ProxyHost.Trim(), ProxyPort ?? 0, NullIfBlank(ProxyUserName)) : null,
            EffectivePasswordSource,
            PasswordIsCommand ? PasswordCommand.Trim() : null)
        {
            TlsKeyPassphrase = SupportsTlsKeyPassphrase ? NullIfBlank(TlsKeyPassphrase) : null,
        };
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync(CancellationToken ct)
    {
        var profile = BuildProfile(withSsh: true);
        // A profile that asks or runs a command keeps no stored password: the leftover is deleted.
        var password = !PasswordIsStored ? "" : Password == (_loadedPassword ?? "") ? null : Password;
        var sshSecret = SshSecret == (_loadedSshSecret ?? "") ? null : SshSecret;
        await _store.SaveAsync(profile with { TlsKeyPassphrase = null }, password, sshSecret, ct);
        var passphrase = !SupportsTlsKeyPassphrase ? "" : TlsKeyPassphrase == (_loadedTlsPassphrase ?? "") ? null : TlsKeyPassphrase;
        await _store.SaveTlsKeyPassphraseAsync(profile.Id, passphrase, ct);
        var proxySecret = !UseProxy || string.IsNullOrWhiteSpace(ProxyUserName) ? "" : ProxyPassword == (_loadedProxySecret ?? "") ? null : ProxyPassword;
        await _store.SaveProxySecretAsync(profile.Id, proxySecret, ct);
        Saved?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task TestAsync(CancellationToken ct)
    {
        try
        {
            var password = EffectivePasswordSource switch
            {
                PasswordSource.Ask => await _dialogs.PromptPasswordAsync(Name),
                PasswordSource.Command => await RunPasswordCommandAsync(ct),
                _ => NullIfBlank(Password),
            };
            if (password is null && EffectivePasswordSource != PasswordSource.Stored)
                return; // the user cancelled the prompt or declined the command: nothing was tried, nothing to report
            var profile = BuildProfile(withSsh: false);
            // The test ignores SSH, and with SSH the proxy carries the SSH connection, so it is only tried for a direct connection.
            await using var tunnel = profile.Proxy is { } proxy && !UseSsh
                ? await _proxies.OpenAsync(proxy, proxy.UserName is null ? null : NullIfBlank(ProxyPassword), profile.Host ?? "", profile.Port ?? 0, ct)
                : null;
            if (tunnel is not null)
                profile = profile with { Host = "127.0.0.1", Port = tunnel.LocalPort, TlsServerName = profile.TlsServerName ?? profile.Host };
            await using var connection = await _providers.Get(Dialect).OpenAsync(profile, password, ct);
            TestStatus = UiText.Format("connections.testStatusOk", connection.ServerVersion);
            TestServerVersion = connection.ServerVersion;
            TestSucceeded = true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var detail = Dialect == DatabaseDialect.Informix ? ex.Message + "\n" + UiText.Get("connections.informixHint") : ex.Message;
            TestStatus = UiText.Format("connections.testStatusFailed", detail);
            TestServerVersion = null;
            TestSucceeded = false;
            await _dialogs.ShowErrorAsync(UiText.Get("connections.testFailedTitle"), detail);
        }
    }

    [RelayCommand]
    private async Task TestPasswordCommandAsync(CancellationToken ct)
    {
        try
        {
            if (await RunPasswordCommandAsync(ct) is null) // the password itself is never shown
                return;
            TestStatus = UiText.Get("connections.passwordCommandOk");
            TestServerVersion = null;
            TestSucceeded = true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            TestStatus = ex.Message;
            TestServerVersion = null;
            TestSucceeded = false;
            await _dialogs.ShowErrorAsync(UiText.Get("connections.testFailedTitle"), ex.Message);
        }
    }

    /// <summary>Runs the typed command after the user saw it (once per profile and command line), as a session does.</summary>
    /// <returns>The password, or <see langword="null"/> when the user declined to run the command.</returns>
    private async Task<string?> RunPasswordCommandAsync(CancellationToken ct)
    {
        var command = PasswordCommand.Trim();
        if (_existing is null || !await _store.IsCommandTrustedAsync(_existing.Id, command, ct))
        {
            if (!await _dialogs.ConfirmAsync(UiText.Get("password.command.confirm.title"), UiText.Format("password.command.confirm", Name, command)))
                return null;
            if (_existing is not null)
                await _store.TrustCommandAsync(_existing.Id, command, ct);
        }

        try
        {
            return await _commandRunner.RunAsync(command, ct);
        }
        catch (PasswordCommandException ex)
        {
            throw new InvalidOperationException(UiText.Format("password.command.failed", ex.Failure.ToString(), ex.Detail ?? "", ex.StandardError ?? ""));
        }
    }
}
