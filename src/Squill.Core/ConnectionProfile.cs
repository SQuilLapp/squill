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
    SshTunnelSettings? Ssh);
