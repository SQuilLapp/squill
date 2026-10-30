using CommunityToolkit.Mvvm.Input;
using Squill.Presentation.Results;

namespace Squill.Presentation.Query;

/// <summary>One result of a run: either a grid of rows or a message.</summary>
public sealed partial class ResultViewModel : ViewModelBase
{
    private readonly IDialogService _dialogs;

    private ResultViewModel(ResultKind kind, string title, StreamingGridDataSource? grid, string text, IDialogService dialogs)
    {
        _dialogs = dialogs;
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

    internal static ResultViewModel ForGrid(string title, StreamingGridDataSource grid, IDialogService dialogs) => new(ResultKind.Grid, title, grid, "", dialogs);

    internal static ResultViewModel ForMessage(string title, string text, IDialogService dialogs) => new(ResultKind.Message, title, null, text, dialogs);

    [RelayCommand]
    private async Task ExportAsync(IResultExporter exporter)
    {
        ArgumentNullException.ThrowIfNull(exporter);
        if (Grid is null)
            return;
        try
        {
            var path = await _dialogs.PickSaveFileAsync(Title, exporter.FileExtension);
            if (path is null)
                return;
            await using var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            await exporter.ExportAsync(Grid, file, CancellationToken.None);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            await _dialogs.ShowErrorAsync("Export failed", ex.Message);
        }
    }
}
