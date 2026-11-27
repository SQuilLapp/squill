namespace Squill.Core;

/// <summary>Persisted description of a database connection; never holds a password.</summary>
public sealed record ConnectionProfile(
    Guid Id,
    string Name,
    DatabaseDialect Dialect,
    string? Host,
    int? Port,
    string? Database,
    string? UserName,
    string? FilePath,
    SshTunnelSettings? Ssh,
    ConnectionEnvironment Environment = ConnectionEnvironment.None,
    string? Folder = null,
    TlsMode Tls = TlsMode.Default,
    bool TrustServerCertificate = false,
    int ConnectTimeoutSeconds = 15,
    int CommandTimeoutSeconds = 0,
    bool MariaDb = false,
    string? WalletPath = null,
    string? TlsCaFile = null,
    string? TlsClientCertFile = null,
    string? TlsClientKeyFile = null,
    string? TlsServerName = null,
    ProxySettings? Proxy = null)
{
    /// <summary>Passphrase of the client key, read from the secret store when a session opens; never persisted in the profile file.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string? TlsKeyPassphrase { get; init; }

    /// <summary>Largest accepted timeout, in seconds (one day).</summary>
    public const int MaxTimeoutSeconds = 86400;

    /// <summary>Checks the timeouts and the TLS files of this profile.</summary>
    /// <returns>One error issue per timeout outside 0 to <see cref="MaxTimeoutSeconds"/>, per missing TLS file and for a client key without a client certificate; empty when valid.</returns>
    public IReadOnlyList<Design.DesignIssue> ValidateOptions()
    {
        List<Design.DesignIssue> issues = [];
        Check(ConnectTimeoutSeconds, CoreMessageCodes.ConnectionConnectTimeoutRange);
        Check(CommandTimeoutSeconds, CoreMessageCodes.ConnectionCommandTimeoutRange);
        CheckFile(TlsCaFile);
        CheckFile(TlsClientCertFile);
        CheckFile(TlsClientKeyFile);
        if (!string.IsNullOrEmpty(TlsClientKeyFile) && string.IsNullOrEmpty(TlsClientCertFile))
            issues.Add(new Design.DesignIssue(Design.DesignIssueSeverity.Error, "A client key needs a client certificate.", null, CoreMessageCodes.ConnectionTlsKeyWithoutCertificate, []));
        return issues;

        void CheckFile(string? path)
        {
            if (!string.IsNullOrEmpty(path) && !File.Exists(path))
                issues.Add(new Design.DesignIssue(Design.DesignIssueSeverity.Error, "A TLS file does not exist.", null, CoreMessageCodes.ConnectionTlsFileMissing, [path]));
        }

        void Check(int value, string code)
        {
            if (value is < 0 or > MaxTimeoutSeconds)
                issues.Add(new Design.DesignIssue(Design.DesignIssueSeverity.Error, "The timeout must be between 0 and 86400 seconds.", null, code, [MaxTimeoutSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)]));
        }
    }
}
