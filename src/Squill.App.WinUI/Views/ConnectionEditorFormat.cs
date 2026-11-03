using System.Globalization;
using Squill.Core;
using Squill.Presentation.Connections;

namespace Squill.App.WinUI.Views;

/// <summary>Choices and texts of the connection editor window that do not need a UI thread.</summary>
internal static class ConnectionEditorFormat
{
    /// <summary>Gets the engine tiles, in the order drawn.</summary>
    public static DatabaseDialect[] Engines { get; } =
        [DatabaseDialect.PostgreSql, DatabaseDialect.MySql, DatabaseDialect.SqlServer, DatabaseDialect.Sqlite];

    /// <summary>Gets the environment tiles, in the order drawn (2 x 2).</summary>
    public static ConnectionEnvironment[] Environments { get; } =
        [ConnectionEnvironment.Production, ConnectionEnvironment.Staging, ConnectionEnvironment.Development, ConnectionEnvironment.Local];

    /// <summary>Gets the two letters and the style suffix (<c>EngineBadge{Suffix}Style</c>) of an engine.</summary>
    /// <param name="dialect">The engine.</param>
    /// <returns>The badge letters and the style suffix.</returns>
    public static (string Letters, string Suffix) Badge(DatabaseDialect dialect) => dialect switch
    {
        DatabaseDialect.PostgreSql => ("PG", "Postgres"),
        DatabaseDialect.MySql => ("My", "Mysql"),
        DatabaseDialect.SqlServer => ("MS", "Sqlserver"),
        _ => ("SL", "Sqlite"),
    };

    /// <summary>Gets the display name of an engine.</summary>
    /// <param name="dialect">The engine.</param>
    /// <returns>The name drawn under the badge of the tile.</returns>
    public static string EngineName(DatabaseDialect dialect) => dialect switch
    {
        DatabaseDialect.PostgreSql => "PostgreSQL",
        DatabaseDialect.MySql => "MySQL",
        DatabaseDialect.SqlServer => "SQL Server",
        _ => "SQLite",
    };

    /// <summary>Gets the catalog key of an environment tile label.</summary>
    /// <param name="environment">The environment.</param>
    /// <returns>The catalog key.</returns>
    public static string EnvironmentKey(ConnectionEnvironment environment) => environment switch
    {
        ConnectionEnvironment.Production => "connections.envProduction",
        ConnectionEnvironment.Staging => "connections.envStaging",
        ConnectionEnvironment.Development => "connections.envDevelopment",
        _ => "connections.envLocal",
    };

    /// <summary>Lists the existing folders for the folder combo box.</summary>
    /// <param name="groups">The grouped connections.</param>
    /// <returns>The folder names, without the ungrouped group.</returns>
    public static string[] Folders(IEnumerable<ConnectionGroup> groups) => [.. groups.Where(g => g.Folder is not null).Select(g => g.Folder!)];

    /// <summary>Parses a port field.</summary>
    /// <param name="text">The field text.</param>
    /// <returns>The port, or <see langword="null"/> when the text is not a number.</returns>
    public static int? ParsePort(string text) => int.TryParse(text, CultureInfo.InvariantCulture, out var port) ? port : null;

    /// <summary>Formats a port for a field.</summary>
    /// <param name="port">The port, or <see langword="null"/>.</param>
    /// <returns>The digits, or an empty string.</returns>
    public static string PortText(int? port) => port?.ToString(CultureInfo.InvariantCulture) ?? "";

    /// <summary>Builds the one-line summary of the SSH card.</summary>
    /// <param name="host">The SSH host.</param>
    /// <param name="port">The SSH port.</param>
    /// <param name="user">The SSH account.</param>
    /// <returns><c>host:port · user</c>.</returns>
    public static string SshSummary(string host, int? port, string user) => $"{host}:{PortText(port)} · {user}";
}
