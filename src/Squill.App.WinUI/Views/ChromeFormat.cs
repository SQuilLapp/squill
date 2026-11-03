using System.Windows.Input;
using Squill.Core;
using Squill.Presentation.Localization;

namespace Squill.App.WinUI.Views;

/// <summary>Texts and lookups of the window chrome that do not need a UI thread.</summary>
internal static class ChromeFormat
{
    /// <summary>Builds the connection segment of the status bar.</summary>
    /// <param name="profile">The active profile, or <see langword="null"/> when not connected.</param>
    /// <returns><c>user@name / database</c>, each part present only when the profile has it.</returns>
    public static string Connection(ConnectionProfile? profile)
    {
        if (profile is null)
            return UiText.Get("status.notConnected");
        var name = string.IsNullOrEmpty(profile.UserName) ? profile.Name : $"{profile.UserName}@{profile.Name}";
        return string.IsNullOrEmpty(profile.Database) ? name : $"{name} / {profile.Database}";
    }

    /// <summary>Chooses the look of the environment block of the status bar.</summary>
    /// <param name="environment">The environment of the active connection.</param>
    /// <returns>The brush name suffix (<c>SquillEnv{Suffix}Brush</c>), the catalog key of the label and whether to draw the warning icon; <see langword="null"/> for <see cref="ConnectionEnvironment.None"/>.</returns>
    public static (string Suffix, string LabelKey, bool Warning)? Environment(ConnectionEnvironment environment) => environment switch
    {
        ConnectionEnvironment.None => null,
        ConnectionEnvironment.Production => ("Prod", "status.production", true),
        ConnectionEnvironment.Staging => ("Staging", "status.staging", false),
        ConnectionEnvironment.Development => ("Dev", "status.dev", false),
        _ => ("Local", "status.local", false),
    };

    /// <summary>Formats the cursor position segment.</summary>
    /// <param name="text">The editor text.</param>
    /// <param name="caret">The caret offset, clamped to the text.</param>
    /// <returns>The localized <c>Ln, Col</c> text, both numbers starting at 1.</returns>
    public static string Position(string text, int caret)
    {
        var before = text.AsSpan(0, Math.Clamp(caret, 0, text.Length));
        var lineStart = before.LastIndexOf('\n') + 1;
        return UiText.Format("status.position", before.Count('\n') + 1, before.Length - lineStart + 1);
    }

    /// <summary>Tells whether a menu item bound to a command is usable.</summary>
    /// <param name="command">The command, or <see langword="null"/> when nothing can run it (no tab, no connection).</param>
    /// <param name="parameter">The command parameter.</param>
    /// <returns><see langword="true"/> when the command exists and can execute.</returns>
    public static bool IsEnabled(ICommand? command, object? parameter) => command?.CanExecute(parameter) == true;
}
