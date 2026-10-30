using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Squill.Core;
using Squill.Data;
using Squill.Platform.Profiles;

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

    /// <summary>Creates an editor for a new profile, or for <paramref name="existing"/> when it is not <see langword="null"/>.</summary>
    /// <param name="existing">Profile to edit, or <see langword="null"/> to create one.</param>
    /// <param name="store">Where the profile is saved.</param>
    /// <param name="providers">Used by the connection test.</param>
    /// <param name="dialogs">Used to report test failures.</param>
    public ConnectionEditorViewModel(ConnectionProfile? existing, IProfileStore store, ProviderRegistry providers, IDialogService dialogs)
    {
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
        Host = existing.Host ?? "";
        Port = existing.Port;
        Database = existing.Database ?? "";
        UserName = existing.UserName ?? "";
        FilePath = existing.FilePath ?? "";
        if (existing.Ssh is { } ssh)
        {
            UseSsh = true;
            SshHost = ssh.Host;
            SshPort = ssh.Port;
            SshUserName = ssh.UserName;
            SshAuthMethod = ssh.AuthMethod;
            SshPrivateKeyPath = ssh.PrivateKeyPath ?? "";
        }
    }

    /// <summary>Raised after the profile has been saved.</summary>
    public event EventHandler? Saved;

    /// <summary>Gets or sets the display name of the profile.</summary>
    [ObservableProperty]
    public partial string Name { get; set; } = "";

    /// <summary>Gets or sets the database engine; changing it switches the default port.</summary>
    [ObservableProperty]
    public partial DatabaseDialect Dialect { get; set; } = DatabaseDialect.PostgreSql;

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

    /// <summary>Gets or sets the SQLite database file path.</summary>
    [ObservableProperty]
    public partial string FilePath { get; set; } = "";

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

    /// <summary>Gets the outcome of the last connection test, or <see langword="null"/> before any test.</summary>
    [ObservableProperty]
    public partial string? TestStatus { get; private set; }

    /// <summary>Gets the default port of an engine.</summary>
    /// <param name="dialect">The engine.</param>
    /// <returns>The port, or <see langword="null"/> for SQLite.</returns>
    public static int? DefaultPort(DatabaseDialect dialect) => dialect switch
    {
        DatabaseDialect.PostgreSql => 5432,
        DatabaseDialect.MySql => 3306,
        DatabaseDialect.SqlServer => 1433,
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
        Password = _loadedPassword ?? "";
        SshSecret = _loadedSshSecret ?? "";
    }

    /// <summary>Checks the current field values.</summary>
    /// <returns>One message per problem; empty when the profile can be saved.</returns>
    public IReadOnlyList<string> Validate()
    {
        List<string> errors = [];
        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Name is required.");
        if (Dialect == DatabaseDialect.Sqlite)
        {
            if (string.IsNullOrWhiteSpace(FilePath))
                errors.Add("File path is required.");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(Host))
                errors.Add("Host is required.");
            if (Port is { } port and (< 1 or > 65535))
                errors.Add("Port must be between 1 and 65535.");
        }

        if (UseSsh)
        {
            if (string.IsNullOrWhiteSpace(SshHost))
                errors.Add("SSH host is required.");
            if (SshPort is not (>= 1 and <= 65535))
                errors.Add("SSH port must be between 1 and 65535.");
            if (string.IsNullOrWhiteSpace(SshUserName))
                errors.Add("SSH user name is required.");
            if (SshAuthMethod == SshAuthMethod.PrivateKey && string.IsNullOrWhiteSpace(SshPrivateKeyPath))
                errors.Add("SSH private key path is required.");
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
        if (Port is null || Port == DefaultPort(oldValue))
            Port = DefaultPort(newValue);
    }

    private bool CanSave() => Validate().Count == 0;

    private static string? NullIfBlank(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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
            ssh);
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync(CancellationToken ct)
    {
        var profile = BuildProfile(withSsh: true);
        var password = Password == (_loadedPassword ?? "") ? null : Password;
        var sshSecret = SshSecret == (_loadedSshSecret ?? "") ? null : SshSecret;
        await _store.SaveAsync(profile, password, sshSecret, ct);
        Saved?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task TestAsync(CancellationToken ct)
    {
        try
        {
            var profile = BuildProfile(withSsh: false);
            await using var connection = await _providers.Get(Dialect).OpenAsync(profile, NullIfBlank(Password), ct);
            TestStatus = $"Connected. Server version: {connection.ServerVersion}";
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            TestStatus = $"Connection failed: {ex.Message}";
            await _dialogs.ShowErrorAsync("Connection test failed", ex.Message);
        }
    }
}
