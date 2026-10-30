using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Squill.Platform.Storage;
using Squill.Presentation.Workspace;

namespace Squill.Presentation.Panels;

/// <summary>Lists past queries, newest first, filtered by text, and reopens them in new tabs.</summary>
/// <param name="store">Where the history is read and cleared.</param>
/// <param name="workspace">Receives the tab opened for an entry.</param>
/// <param name="dialogs">Used for the clear confirmation.</param>
public sealed partial class HistoryViewModel(IQueryHistoryStore store, WorkspaceViewModel workspace, IDialogService dialogs) : ViewModelBase
{
    private const int MaxLoaded = 500;

    private IReadOnlyList<HistoryEntry> _all = [];

    /// <summary>Gets the loaded entries matching <see cref="FilterText"/>, newest first.</summary>
    public ObservableCollection<HistoryEntry> Entries { get; } = [];

    /// <summary>Gets or sets the text the SQL of an entry must contain, ignoring case; empty shows everything.</summary>
    [ObservableProperty]
    public partial string FilterText { get; set; } = "";

    partial void OnFilterTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        Entries.Clear();
        foreach (var entry in _all)
        {
            if (FilterText.Length == 0 || entry.Sql.Contains(FilterText, StringComparison.OrdinalIgnoreCase))
                Entries.Add(entry);
        }
    }

    [RelayCommand]
    private async Task LoadAsync(CancellationToken ct)
    {
        var entries = await store.ListAsync(MaxLoaded, ct);
        _all = [.. entries.Reverse()];
        ApplyFilter();
    }

    [RelayCommand]
    private async Task ClearAsync(CancellationToken ct)
    {
        if (!await dialogs.ConfirmAsync("Clear history", "Delete the whole query history?"))
            return;
        await store.ClearAsync(ct);
        _all = [];
        Entries.Clear();
    }

    [RelayCommand]
    private void Open(HistoryEntry? entry)
    {
        if (entry is not null)
            workspace.OpenInNewTab(entry.Sql);
    }
}
