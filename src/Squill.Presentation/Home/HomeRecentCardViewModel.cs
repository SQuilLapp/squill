using CommunityToolkit.Mvvm.Input;
using Squill.Core;
using Squill.Presentation.Localization;

namespace Squill.Presentation.Home;

/// <summary>One recent connection of the home page.</summary>
public sealed class HomeRecentCardViewModel : ViewModelBase
{
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

    /// <summary>Gets the shortcut that opens the card, <c>Ctrl+1</c> to <c>Ctrl+6</c>.</summary>
    public string Shortcut { get; }

    /// <summary>Gets the command that connects and records the connection as recent.</summary>
    public IAsyncRelayCommand OpenCommand { get; }

    private static string Describe(ConnectionProfile p) =>
        $"{(p.UserName is { Length: > 0 } u ? u + "@" : "")}{p.Host}{(p.Port is { } port ? ":" + port : "")}{(p.Database is { Length: > 0 } d ? "/" + d : "")}";
}
