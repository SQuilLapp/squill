using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Squill.Presentation.Editing;
using Squill.Presentation.Localization;
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
        Header = title;
        if (grid is not null)
        {
            ReadOnlyReason = "loading";
            grid.Changed += (_, _) => UpdateHeader();
        }
    }

    /// <summary>Gets the tab header: the title, then the loaded row count and a <c>+</c> when the row limit truncated the result.</summary>
    [ObservableProperty]
    public partial string Header { get; private set; }

    /// <summary>Gets the loaded row count shown after the title in a result tab: grouped digits and a <c>+</c> when the row limit truncated the result; empty for a message.</summary>
    [ObservableProperty]
    public partial string CountText { get; private set; } = "";

    /// <summary>Gets the editor of a single-table result, otherwise <see langword="null"/>.</summary>
    [ObservableProperty]
    public partial ResultEditorViewModel? Editor { get; private set; }

    /// <summary>Gets the keyboard and cell-edit logic shared by the heads; set together with <see cref="Editor"/>.</summary>
    public GridEditController? EditController { get; private set; }

    /// <summary>Gets the selection the heads and <see cref="EditController"/> share; set together with <see cref="Editor"/>.</summary>
    public GridSelection? Selection { get; private set; }

    /// <summary>Gets why the grid cannot be edited (<c>loading</c> while it streams), or <see langword="null"/> when it is editable or a message.</summary>
    [ObservableProperty]
    public partial string? ReadOnlyReason { get; private set; }

    /// <summary>Gets the grid the heads bind: the editor's grid when there is one, otherwise <see cref="Grid"/>.</summary>
    public IGridDataSource? DisplayGrid => Editor?.Grid ?? (IGridDataSource?)Grid;

    /// <summary>Gets whether the result holds uncommitted grid edits.</summary>
    public bool HasPendingChanges => Editor?.HasPendingChanges == true;

    /// <summary>Gets whether this result is a grid or a message.</summary>
    public ResultKind Kind { get; }

    /// <summary>Gets the tab title, such as <c>Result 1</c>.</summary>
    public string Title { get; private set; }

    /// <summary>Gets the streaming rows of a <see cref="ResultKind.Grid"/> result, otherwise <see langword="null"/>.</summary>
    public StreamingGridDataSource? Grid { get; }

    /// <summary>Gets the text of a <see cref="ResultKind.Message"/> or <see cref="ResultKind.Messages"/> result, otherwise an empty string.</summary>
    public string Text { get; }

    internal void SetEditor(ResultEditorViewModel editor)
    {
        Selection = new GridSelection();
        EditController = new GridEditController(editor, Selection);
        Editor = editor;
        ReadOnlyReason = null;
        editor.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ResultEditorViewModel.HasPendingChanges))
            {
                OnPropertyChanged(nameof(HasPendingChanges));
                UpdateHeader();
            }
        };
        OnPropertyChanged(nameof(DisplayGrid));
        UpdateHeader();
    }

    internal void SetTitle(string title)
    {
        Title = title;
        UpdateHeader();
    }

    internal void SetReadOnly(string reason) => ReadOnlyReason = reason;

    private void UpdateHeader()
    {
        CountText = Grid is null ? "" : DisplayFormat.Count(Grid.RowCount) + (Grid.IsTruncated ? "+" : "");
        Header = Grid is null
            ? Title
            : $"{Title} ({CountText} rows){(HasPendingChanges ? " *" : "")}";
    }

    internal static ResultViewModel ForGrid(string title, StreamingGridDataSource grid, IDialogService dialogs) => new(ResultKind.Grid, title, grid, "", dialogs);

    internal static ResultViewModel ForMessage(string title, string text, IDialogService dialogs) => new(ResultKind.Message, title, null, text, dialogs);

    internal static ResultViewModel ForMessages(string title, string text, IDialogService dialogs) => new(ResultKind.Messages, title, null, text, dialogs);

    [RelayCommand]
    private async Task ExportAsync(IResultExporter exporter)
    {
        ArgumentNullException.ThrowIfNull(exporter);
        if (Grid is null)
            return;
        if (!Grid.IsComplete)
        {
            await _dialogs.ShowErrorAsync(UiText.Get("results.exportFailed"), UiText.Get("results.exportLoading"));
            return;
        }

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
            await _dialogs.ShowErrorAsync(UiText.Get("results.exportFailed"), ex.Message);
        }
    }
}
