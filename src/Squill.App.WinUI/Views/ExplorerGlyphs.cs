using Microsoft.UI.Xaml;
using Squill.Presentation.Explorer;

namespace Squill.App.WinUI.Views;

/// <summary>Functions used by the compiled bindings of the explorer templates.</summary>
public static class ExplorerGlyphs
{
    /// <summary>Maps a node kind to the short glyph drawn before its title.</summary>
    /// <param name="kind">The node kind.</param>
    /// <returns>The glyph.</returns>
    public static string Glyph(ExplorerNodeKind kind) => kind switch
    {
        ExplorerNodeKind.Schema => "S",
        ExplorerNodeKind.Table => "T",
        ExplorerNodeKind.View => "V",
        ExplorerNodeKind.Column => "c",
        ExplorerNodeKind.Index => "i",
        ExplorerNodeKind.ForeignKey => "fk",
        _ => ">",
    };

    /// <summary>Maps a boolean to a visibility.</summary>
    /// <param name="visible">Whether the element is shown.</param>
    /// <returns><see cref="Visibility.Visible"/> or <see cref="Visibility.Collapsed"/>.</returns>
    public static Visibility Show(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;
}
