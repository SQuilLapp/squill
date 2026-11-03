using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Squill.Core;
using Squill.Presentation.Explorer;

namespace Squill.App.WinUI.Views;

/// <summary>Functions used by the compiled bindings of the explorer templates.</summary>
public static class ExplorerGlyphs
{
    /// <summary>Maps a node kind to the short glyph drawn before its title when it has no icon.</summary>
    /// <param name="kind">The node kind.</param>
    /// <returns>The glyph.</returns>
    public static string Glyph(ExplorerNodeKind kind) => kind switch
    {
        ExplorerNodeKind.Column => "c",
        ExplorerNodeKind.Index => "i",
        ExplorerNodeKind.ForeignKey => "fk",
        _ => ">",
    };

    /// <summary>Maps a boolean to a visibility.</summary>
    /// <param name="visible">Whether the element is shown.</param>
    /// <returns><see cref="Visibility.Visible"/> or <see cref="Visibility.Collapsed"/>.</returns>
    public static Visibility Show(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Shows an element only for table nodes.</summary>
    /// <param name="kind">The node kind.</param>
    /// <returns>The visibility.</returns>
    public static Visibility ShowTable(ExplorerNodeKind kind) => Show(kind == ExplorerNodeKind.Table);

    /// <summary>Shows an element only for view nodes.</summary>
    /// <param name="kind">The node kind.</param>
    /// <returns>The visibility.</returns>
    public static Visibility ShowView(ExplorerNodeKind kind) => Show(kind == ExplorerNodeKind.View);

    /// <summary>Shows an element only for schema and connection folder nodes.</summary>
    /// <param name="kind">The node kind.</param>
    /// <returns>The visibility.</returns>
    public static Visibility ShowFolderIcon(ExplorerNodeKind kind) => Show(kind is ExplorerNodeKind.Schema or ExplorerNodeKind.ConnectionFolder);

    /// <summary>Shows an element only for database nodes.</summary>
    /// <param name="kind">The node kind.</param>
    /// <returns>The visibility.</returns>
    public static Visibility ShowDatabase(ExplorerNodeKind kind) => Show(kind == ExplorerNodeKind.Database);

    /// <summary>Shows an element only for connection nodes.</summary>
    /// <param name="kind">The node kind.</param>
    /// <returns>The visibility.</returns>
    public static Visibility ShowConnection(ExplorerNodeKind kind) => Show(kind == ExplorerNodeKind.Connection);

    /// <summary>Shows an element only for the kinds drawn with a glyph rather than an icon.</summary>
    /// <param name="kind">The node kind.</param>
    /// <returns>The visibility.</returns>
    public static Visibility ShowPlain(ExplorerNodeKind kind) =>
        Show(kind is ExplorerNodeKind.Column or ExplorerNodeKind.Index or ExplorerNodeKind.ForeignKey);

    /// <summary>Draws connection titles semibold and the objects regular.</summary>
    /// <param name="kind">The node kind.</param>
    /// <returns>The font weight.</returns>
    public static global::Windows.UI.Text.FontWeight TitleWeight(ExplorerNodeKind kind) =>
        kind == ExplorerNodeKind.Connection ? FontWeights.SemiBold : FontWeights.Normal;

    /// <summary>Picks the engine badge style of a connection.</summary>
    /// <param name="profile">The connection, or <see langword="null"/>.</param>
    /// <returns>The style.</returns>
    public static Style EngineStyle(ConnectionProfile? profile) => StyleOf(profile?.Dialect switch
    {
        DatabaseDialect.PostgreSql => "EngineBadgePostgresStyle",
        DatabaseDialect.MySql => "EngineBadgeMysqlStyle",
        DatabaseDialect.SqlServer => "EngineBadgeSqlserverStyle",
        DatabaseDialect.Sqlite => "EngineBadgeSqliteStyle",
        _ => "EngineBadgeStyle",
    });

    /// <summary>Picks the environment pill style of a connection.</summary>
    /// <param name="profile">The connection, or <see langword="null"/>.</param>
    /// <returns>The style.</returns>
    public static Style EnvironmentStyle(ConnectionProfile? profile) => StyleOf(profile?.Environment switch
    {
        ConnectionEnvironment.Production => "BadgeEnvProdStyle",
        ConnectionEnvironment.Staging => "BadgeEnvStagingStyle",
        ConnectionEnvironment.Development => "BadgeEnvDevStyle",
        ConnectionEnvironment.Local => "BadgeEnvLocalStyle",
        _ => "BadgeStyle",
    });

    /// <summary>Looks up an application style.</summary>
    /// <param name="key">The resource key of the style.</param>
    /// <returns>The style.</returns>
    public static Style StyleOf(string key) => (Style)Application.Current.Resources[key];
}
