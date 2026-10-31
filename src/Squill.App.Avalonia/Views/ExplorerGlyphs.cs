using Avalonia.Data.Converters;
using Squill.Presentation.Explorer;

namespace Squill.App.Avalonia.Views;

/// <summary>Converters used by the explorer templates.</summary>
internal static class ExplorerGlyphs
{
    /// <summary>Maps a node kind to the short glyph drawn before its title.</summary>
    public static readonly FuncValueConverter<ExplorerNodeKind, string> Glyph = new(kind => kind switch
    {
        ExplorerNodeKind.Schema => "S",
        ExplorerNodeKind.Table => "T",
        ExplorerNodeKind.View => "V",
        ExplorerNodeKind.Column => "c",
        ExplorerNodeKind.Index => "i",
        ExplorerNodeKind.ForeignKey => "fk",
        _ => ">",
    });
}
