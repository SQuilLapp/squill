namespace Squill.Presentation.Palette;

/// <summary>The search mode of the command palette, selected by the first typed character.</summary>
public enum PaletteMode
{
    /// <summary>No prefix: every provider is searched.</summary>
    All,

    /// <summary>Prefix <c>&gt;</c>: actions only.</summary>
    Actions,

    /// <summary>Prefix <c>#</c>: database objects only.</summary>
    Objects,

    /// <summary>Prefix <c>:</c>: go to a line of the active editor.</summary>
    Line,

    /// <summary>Prefix <c>!</c>: query history only.</summary>
    History,
}
