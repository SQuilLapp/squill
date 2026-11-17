using Squill.Core;

namespace Squill.Presentation.Palette;

/// <summary>What a palette row is.</summary>
public enum PaletteItemKind
{
    /// <summary>A command of the application.</summary>
    Action,

    /// <summary>A schema object of an open connection.</summary>
    SchemaObject,

    /// <summary>A statement of the query history.</summary>
    History,

    /// <summary>A go-to-line request.</summary>
    Line,
}

/// <summary>How a palette row is run.</summary>
public enum PaletteRunMode
{
    /// <summary>Plain Enter.</summary>
    Default,

    /// <summary>The Ctrl+Enter variant: open in a new query tab where that makes sense.</summary>
    NewTab,
}

/// <summary>One row of the command palette, pure data plus the delegate that runs it; the heads map <see cref="IconKey"/> to an icon.</summary>
/// <param name="Kind">The kind of row.</param>
/// <param name="Title">The main text.</param>
/// <param name="Detail">The muted text after the title: kind and schema, category, age and duration.</param>
/// <param name="Right">The right-hand text, the connection name for an object; otherwise <see langword="null"/>.</param>
/// <param name="Environment">The environment of the connection, drawn as a square, or <see langword="null"/>.</param>
/// <param name="Shortcut">The shortcut to display, or <see langword="null"/>.</param>
/// <param name="IconKey">A logical icon key such as <c>table</c>, <c>action</c> or <c>history</c>.</param>
/// <param name="Ranges">The matched runs of <paramref name="Title"/>, for highlighting.</param>
/// <param name="IsEnabled">Whether <paramref name="Run"/> may be called.</param>
/// <param name="Score">The rank of the match, higher is better.</param>
/// <param name="Run">Runs the row; the palette closes before calling it.</param>
public sealed record PaletteItem(
    PaletteItemKind Kind,
    string Title,
    string Detail,
    string? Right,
    ConnectionEnvironment? Environment,
    string? Shortcut,
    string IconKey,
    IReadOnlyList<(int Start, int Length)> Ranges,
    bool IsEnabled,
    int Score,
    Func<PaletteRunMode, CancellationToken, ValueTask> Run);
