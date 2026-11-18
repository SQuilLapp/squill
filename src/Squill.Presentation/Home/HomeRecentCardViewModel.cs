using CommunityToolkit.Mvvm.Input;
using Squill.Core;
using Squill.Presentation.Localization;

namespace Squill.Presentation.Home;

/// <summary>One recent connection of the home page.</summary>
public sealed class HomeRecentCardViewModel : ViewModelBase
{
    private int _tabs;
    private string? _version;

    internal HomeRecentCardViewModel(ConnectionProfile profile, string lastOpened, string shortcut, Func<Task> open)
    {
        Profile = profile;
        Name = profile.Name;
        EngineLetters = profile.Dialect switch
        {
            DatabaseDialect.PostgreSql => "PG",
            DatabaseDialect.MySql => "My",
            DatabaseDialect.SqlServer => "MS",
            DatabaseDialect.Sqlite => "SL",
            _ => "",
        };
        EngineName = profile.Dialect switch
        {
            DatabaseDialect.PostgreSql => "PostgreSQL",
            DatabaseDialect.MySql => "MySQL",
            DatabaseDialect.SqlServer => "SQL Server",
            DatabaseDialect.Sqlite => "SQLite",
            _ => "",
        };
        Environment = profile.Environment;
        Subtitle = profile.FilePath is { Length: > 0 } path ? path : Describe(profile);
        LastOpened = lastOpened;
        Shortcut = shortcut;
        OpenCommand = new AsyncRelayCommand(open);
    }

    /// <summary>Gets the connection this card opens.</summary>
    public ConnectionProfile Profile { get; }

    /// <summary>Gets the connection name.</summary>
    public string Name { get; }

    /// <summary>Gets the two-letter engine badge, as in the explorer.</summary>
    public string EngineLetters { get; }

    /// <summary>Gets the environment, for the pill colour; <see cref="ConnectionEnvironment.None"/> draws no pill.</summary>
    public ConnectionEnvironment Environment { get; }

    /// <summary>Gets the pill text (<c>PROD</c>, <c>STAGING</c>, <c>DEV</c>, <c>LOCAL</c>), empty without environment.</summary>
    public string EnvironmentText => Environment switch
    {
        ConnectionEnvironment.Production => UiText.Get("home.env.prod"),
        ConnectionEnvironment.Staging => UiText.Get("home.env.staging"),
        ConnectionEnvironment.Development => UiText.Get("home.env.dev"),
        ConnectionEnvironment.Local => UiText.Get("home.env.local"),
        _ => "",
    };

    /// <summary>Gets <c>user@host:port/database</c> or the file path; never the password.</summary>
    public string Subtitle { get; }

    /// <summary>Gets when the connection was last opened, as relative text.</summary>
    public string LastOpened { get; }

    /// <summary>Gets the engine name, <c>PostgreSQL</c>, <c>MySQL</c>, <c>SQL Server</c> or <c>SQLite</c>.</summary>
    public string EngineName { get; } = "";

    /// <summary>Gets the engine name followed by the live server version when the connection is open, then <c>·</c> and <see cref="LastOpened"/>.</summary>
    public string Details => $"{(_version is { Length: > 0 } v ? EngineName + " " + v : EngineName)} · {LastOpened}";

    /// <summary>Gets the open-tab count as text (<c>3 onglets</c>, <c>1 onglet</c>), <c>-</c> when no tab is open.</summary>
    public string OpenTabsText => _tabs == 0 ? "-" : UiText.FormatPlural("home.tabs", _tabs);

    /// <summary>Updates the live values shown on the card.</summary>
    /// <param name="tabs">Number of query tabs open on this connection.</param>
    /// <param name="version">Server version of the open connection, or <see langword="null"/> when it is not connected.</param>
    public void ApplyLive(int tabs, string? version)
    {
        _tabs = tabs;
        _version = version?.Split(' ')[0];
        OnPropertyChanged(nameof(Details));
        OnPropertyChanged(nameof(OpenTabsText));
    }

    /// <summary>Gets the shortcut that opens the card, <c>Ctrl+1</c> to <c>Ctrl+6</c>.</summary>
    public string Shortcut { get; }

    /// <summary>Gets the command that connects and records the connection as recent.</summary>
    public IAsyncRelayCommand OpenCommand { get; }

    private static string Describe(ConnectionProfile p) =>
        $"{(p.UserName is { Length: > 0 } u ? u + "@" : "")}{p.Host}{(p.Port is { } port ? ":" + port : "")}{(p.Database is { Length: > 0 } d ? "/" + d : "")}";
}
