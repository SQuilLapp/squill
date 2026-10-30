using Squill.Presentation.Results;

namespace Squill.Presentation.Query;

/// <summary>One result of a run: either a grid of rows or a message.</summary>
public sealed class ResultViewModel : ViewModelBase
{
    private ResultViewModel(ResultKind kind, string title, StreamingGridDataSource? grid, string text)
    {
        Kind = kind;
        Title = title;
        Grid = grid;
        Text = text;
    }

    /// <summary>Gets whether this result is a grid or a message.</summary>
    public ResultKind Kind { get; }

    /// <summary>Gets the tab title, such as <c>Result 1</c>.</summary>
    public string Title { get; }

    /// <summary>Gets the streaming rows of a <see cref="ResultKind.Grid"/> result, otherwise <see langword="null"/>.</summary>
    public StreamingGridDataSource? Grid { get; }

    /// <summary>Gets the text of a <see cref="ResultKind.Message"/> result, otherwise an empty string.</summary>
    public string Text { get; }

    internal static ResultViewModel ForGrid(string title, StreamingGridDataSource grid) => new(ResultKind.Grid, title, grid, "");

    internal static ResultViewModel ForMessage(string title, string text) => new(ResultKind.Message, title, null, text);
}
