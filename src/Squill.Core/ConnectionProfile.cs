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
    int CommandTimeoutSeconds = 0)
{
    /// <summary>Largest accepted timeout, in seconds (one day).</summary>
    public const int MaxTimeoutSeconds = 86400;

    /// <summary>Checks the timeouts of this profile.</summary>
    /// <returns>One error issue per timeout outside 0 to <see cref="MaxTimeoutSeconds"/>; empty when valid.</returns>
    public IReadOnlyList<Design.DesignIssue> ValidateOptions()
    {
        List<Design.DesignIssue> issues = [];
        Check(ConnectTimeoutSeconds, CoreMessageCodes.ConnectionConnectTimeoutRange);
        Check(CommandTimeoutSeconds, CoreMessageCodes.ConnectionCommandTimeoutRange);
        return issues;

        void Check(int value, string code)
        {
            if (value is < 0 or > MaxTimeoutSeconds)
                issues.Add(new Design.DesignIssue(Design.DesignIssueSeverity.Error, "The timeout must be between 0 and 86400 seconds.", null, code, [MaxTimeoutSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)]));
        }
    }
}
