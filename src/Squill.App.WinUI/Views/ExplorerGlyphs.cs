using Microsoft.UI.Xaml;
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

    /// <summary>Shows an element only for schema nodes.</summary>
    /// <param name="kind">The node kind.</param>
    /// <returns>The visibility.</returns>
    public static Visibility ShowSchema(ExplorerNodeKind kind) => Show(kind == ExplorerNodeKind.Schema);

    /// <summary>Shows an element only for the kinds drawn with a glyph rather than an icon.</summary>
    /// <param name="kind">The node kind.</param>
    /// <returns>The visibility.</returns>
    public static Visibility ShowPlain(ExplorerNodeKind kind) =>
        Show(kind is not (ExplorerNodeKind.Table or ExplorerNodeKind.View or ExplorerNodeKind.Schema));

    /// <summary>Looks up an application style.</summary>
    /// <param name="key">The resource key of the style.</param>
    /// <returns>The style.</returns>
    public static Style StyleOf(string key) => (Style)Application.Current.Resources[key];

    /// <summary>Builds the left margin of an indented connection row.</summary>
    /// <param name="left">The indent in pixels.</param>
    /// <returns>A thickness with only a left side.</returns>
    public static Thickness Indent(double left) => new(left, 0, 0, 0);
}
