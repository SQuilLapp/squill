using Avalonia.Data.Converters;
using Squill.Presentation.Explorer;

namespace Squill.App.Avalonia.Views;

/// <summary>Converters used by the explorer templates.</summary>
internal static class ExplorerGlyphs
{
    /// <summary>Maps a node kind to the short glyph drawn before its title when it has no icon.</summary>
    public static readonly FuncValueConverter<ExplorerNodeKind, string> Glyph = new(kind => kind switch
    {
        ExplorerNodeKind.Column => "c",
        ExplorerNodeKind.Index => "i",
        ExplorerNodeKind.ForeignKey => "fk",
        _ => ">",
    });

    /// <summary>Tells whether a node kind is a table.</summary>
    public static readonly FuncValueConverter<ExplorerNodeKind, bool> IsTable = new(kind => kind == ExplorerNodeKind.Table);

    /// <summary>Tells whether a node kind is a view.</summary>
    public static readonly FuncValueConverter<ExplorerNodeKind, bool> IsView = new(kind => kind == ExplorerNodeKind.View);

    /// <summary>Tells whether a node kind is a schema.</summary>
    public static readonly FuncValueConverter<ExplorerNodeKind, bool> IsSchema = new(kind => kind == ExplorerNodeKind.Schema);

    /// <summary>Tells whether a node kind is drawn with a glyph rather than an icon.</summary>
    public static readonly FuncValueConverter<ExplorerNodeKind, bool> IsPlain =
        new(kind => kind is not (ExplorerNodeKind.Table or ExplorerNodeKind.View or ExplorerNodeKind.Schema));
}
