using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Squill.Core;
using Squill.Data;
using Squill.Platform.Profiles;

namespace Squill.Presentation.Connections;

/// <summary>Lists the saved connection profiles and starts creating, editing and deleting them.</summary>
/// <param name="store">Where profiles are read and deleted.</param>
/// <param name="providers">Handed to the editors for their connection test.</param>
/// <param name="dialogs">Used for the delete confirmation and handed to the editors.</param>
public sealed partial class ConnectionListViewModel(IProfileStore store, ProviderRegistry providers, IDialogService dialogs) : ViewModelBase
{
    /// <summary>Raised when the user asks to edit or create a profile; the view shows the given editor.</summary>
    public event EventHandler<ConnectionEditorViewModel>? EditRequested;

    /// <summary>Gets the saved profiles.</summary>
    public ObservableCollection<ConnectionProfile> Profiles { get; } = [];

    /// <summary>Gets or sets the profile the edit and delete commands act on.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    public partial ConnectionProfile? SelectedProfile { get; set; }

    private bool HasSelection() => SelectedProfile is not null;

    [RelayCommand]
    private async Task LoadAsync(CancellationToken ct)
    {
        var profiles = await store.ListAsync(ct);
        Profiles.Clear();
        foreach (var profile in profiles)
            Profiles.Add(profile);
    }

    [RelayCommand]
    private Task NewAsync(CancellationToken ct) => RequestEditAsync(null, ct);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private Task EditAsync(CancellationToken ct) => RequestEditAsync(SelectedProfile, ct);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task DeleteAsync(CancellationToken ct)
    {
        if (SelectedProfile is not { } profile)
            return;
        if (!await dialogs.ConfirmAsync("Delete connection", $"Delete the connection '{profile.Name}'?"))
            return;
        await store.DeleteAsync(profile.Id, ct);
        Profiles.Remove(profile);
        SelectedProfile = null;
    }

    private async Task RequestEditAsync(ConnectionProfile? profile, CancellationToken ct)
    {
        var editor = new ConnectionEditorViewModel(profile, store, providers, dialogs);
        await editor.LoadSecretsAsync(ct);
        editor.Saved += (_, _) => _ = LoadAsync(CancellationToken.None);
        EditRequested?.Invoke(this, editor);
    }
}
