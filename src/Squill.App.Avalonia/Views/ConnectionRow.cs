using Avalonia;
using Squill.Core;
using Squill.Presentation.Connections;
using Squill.Presentation.Localization;

namespace Squill.App.Avalonia.Views;

/// <summary>One line of the sidebar connection list: a folder header or a connection.</summary>
public sealed class ConnectionRow
{
    private ConnectionRow(string? folder, int count, ConnectionProfile? profile, bool indented)
    {
        Folder = folder;
        Count = count;
        Profile = profile;
        Indented = indented;
    }

    /// <summary>Gets the folder name of a header row; <see langword="null"/> for connection rows.</summary>
    public string? Folder { get; }

    /// <summary>Gets the number of connections of a header row's folder.</summary>
    public int Count { get; }

    /// <summary>Gets the connection of a connection row; <see langword="null"/> for headers.</summary>
    public ConnectionProfile? Profile { get; }

    /// <summary>Gets whether the row sits under a folder header.</summary>
    public bool Indented { get; }

    /// <summary>Gets the left margin that indents connections under their folder.</summary>
    public Thickness Indent => Indented ? new Thickness(16, 0, 0, 0) : default;

    /// <summary>Gets whether the row is a connection (selectable) and not a folder header.</summary>
    public bool IsProfile => Profile is not null;

    /// <summary>Gets whether the row is a folder header.</summary>
    public bool IsHeader => Profile is null;

    /// <summary>Gets the name shown on a connection row.</summary>
    public string Name => Profile?.Name ?? "";

    /// <summary>Gets the two-letter engine badge text.</summary>
    public string EngineLetters => Profile?.Dialect switch
    {
        DatabaseDialect.PostgreSql => "PG",
        DatabaseDialect.MySql => "My",
        DatabaseDialect.SqlServer => "MS",
        DatabaseDialect.Sqlite => "SL",
        _ => "",
    };

    /// <summary>Gets whether the engine is PostgreSQL.</summary>
    public bool IsPostgres => Profile?.Dialect == DatabaseDialect.PostgreSql;

    /// <summary>Gets whether the engine is MySQL.</summary>
    public bool IsMySql => Profile?.Dialect == DatabaseDialect.MySql;

    /// <summary>Gets whether the engine is SQL Server.</summary>
    public bool IsSqlServer => Profile?.Dialect == DatabaseDialect.SqlServer;

    /// <summary>Gets whether the engine is SQLite.</summary>
    public bool IsSqlite => Profile?.Dialect == DatabaseDialect.Sqlite;

    /// <summary>Gets whether the connection has an environment to show as a pill.</summary>
    public bool HasEnvironment => Profile is { Environment: not ConnectionEnvironment.None };

    /// <summary>Gets whether the environment is production.</summary>
    public bool IsProd => Profile?.Environment == ConnectionEnvironment.Production;

    /// <summary>Gets whether the environment is staging.</summary>
    public bool IsStaging => Profile?.Environment == ConnectionEnvironment.Staging;

    /// <summary>Gets whether the environment is development.</summary>
    public bool IsDev => Profile?.Environment == ConnectionEnvironment.Development;

    /// <summary>Gets whether the environment is local.</summary>
    public bool IsLocal => Profile?.Environment == ConnectionEnvironment.Local;

    /// <summary>Gets the caps label of the environment pill.</summary>
    public string EnvironmentText => Profile?.Environment switch
    {
        ConnectionEnvironment.Production => UiText.Get("status.production"),
        ConnectionEnvironment.Staging => UiText.Get("status.staging"),
        ConnectionEnvironment.Development => UiText.Get("status.dev"),
        ConnectionEnvironment.Local => UiText.Get("status.local"),
        _ => "",
    };

    /// <summary>Flattens folder groups into header rows followed by their connections; the ungrouped connections get no header.</summary>
    /// <param name="groups">The groups as <see cref="ConnectionListViewModel.Groups"/> orders them.</param>
    /// <returns>The rows in display order.</returns>
    public static IReadOnlyList<ConnectionRow> Build(IReadOnlyList<ConnectionGroup> groups)
    {
        List<ConnectionRow> rows = [];
        foreach (var group in groups)
        {
            if (group.Folder is not null)
                rows.Add(new ConnectionRow(group.Folder, group.Items.Count, null, false));
            rows.AddRange(group.Items.Select(p => new ConnectionRow(null, 0, p, group.Folder is not null)));
        }

        return rows;
    }
}
