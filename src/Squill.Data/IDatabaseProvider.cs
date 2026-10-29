using Squill.Core;

namespace Squill.Data;

/// <summary>Opens connections to one database engine.</summary>
public interface IDatabaseProvider
{
    /// <summary>The engine this provider serves.</summary>
    DatabaseDialect Dialect { get; }

    /// <summary>Opens a connection for the profile; the password comes from the secret store, never the profile.</summary>
    ValueTask<IDatabaseConnection> OpenAsync(ConnectionProfile profile, string? password, CancellationToken cancellationToken);
}
