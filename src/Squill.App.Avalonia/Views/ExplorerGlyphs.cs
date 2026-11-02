using Avalonia.Data.Converters;
using Avalonia.Media;
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

    /// <summary>Tells whether a node kind is drawn with the folder icon: schemas and connection folders.</summary>
    public static readonly FuncValueConverter<ExplorerNodeKind, bool> IsFolderIcon =
        new(kind => kind is ExplorerNodeKind.Schema or ExplorerNodeKind.ConnectionFolder);

    /// <summary>Tells whether a node kind is a database.</summary>
    public static readonly FuncValueConverter<ExplorerNodeKind, bool> IsDatabase = new(kind => kind == ExplorerNodeKind.Database);

    /// <summary>Tells whether a node kind is a connection.</summary>
    public static readonly FuncValueConverter<ExplorerNodeKind, bool> IsConnection = new(kind => kind == ExplorerNodeKind.Connection);

    /// <summary>Tells whether a node kind is anything but a connection.</summary>
    public static readonly FuncValueConverter<ExplorerNodeKind, bool> IsNotConnection = new(kind => kind != ExplorerNodeKind.Connection);

    /// <summary>Tells whether a node kind is drawn with a glyph rather than an icon.</summary>
    public static readonly FuncValueConverter<ExplorerNodeKind, bool> IsPlain =
        new(kind => kind is ExplorerNodeKind.Column or ExplorerNodeKind.Index or ExplorerNodeKind.ForeignKey);

    /// <summary>Draws connection and folder titles semibold, the objects regular.</summary>
    public static readonly FuncValueConverter<ExplorerNodeKind, FontWeight> TitleWeight =
        new(kind => kind is ExplorerNodeKind.Connection ? FontWeight.SemiBold : FontWeight.Normal);
}
