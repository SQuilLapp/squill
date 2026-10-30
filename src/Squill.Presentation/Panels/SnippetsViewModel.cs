using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using Squill.Platform.Storage;
using Squill.Presentation.Workspace;

namespace Squill.Presentation.Panels;

/// <summary>Lists the saved snippets and saves, inserts, renames and deletes them.</summary>
/// <param name="store">Where snippets are persisted.</param>
/// <param name="workspace">Provides the active tab whose editor is read and modified.</param>
/// <param name="dialogs">Used for the name prompts and the delete confirmation.</param>
public sealed partial class SnippetsViewModel(ISnippetStore store, WorkspaceViewModel workspace, IDialogService dialogs) : ViewModelBase
{
    /// <summary>Gets the saved snippets in saving order.</summary>
    public ObservableCollection<Snippet> Snippets { get; } = [];

    [RelayCommand]
    private async Task LoadAsync(CancellationToken ct)
    {
        var snippets = await store.ListAsync(ct);
        Snippets.Clear();
        foreach (var snippet in snippets)
            Snippets.Add(snippet);
    }

    [RelayCommand]
    private async Task SaveCurrentAsync(CancellationToken ct)
    {
        if (workspace.ActiveTab?.Editor is not { } editor)
            return;
        var sel = editor.Selection;
        var sql = sel.Length > 0 ? editor.Text.Substring(sel.Start, sel.Length) : editor.Text;
        if (string.IsNullOrWhiteSpace(sql))
            return;
        var name = await dialogs.PromptAsync("Save snippet", "Snippet name", null);
        if (string.IsNullOrWhiteSpace(name))
            return;
        await store.SaveAsync(new Snippet(Guid.NewGuid(), name.Trim(), sql), ct);
        await LoadAsync(ct);
    }

    [RelayCommand]
    private void Insert(Snippet? snippet)
    {
        if (snippet is null || workspace.ActiveTab?.Editor is not { } editor)
            return;
        var text = editor.Text;
        var start = Math.Clamp(editor.Selection.Start, 0, text.Length);
        var end = Math.Clamp(editor.Selection.End, start, text.Length);
        editor.Text = string.Concat(text.AsSpan(0, start), snippet.Sql, text.AsSpan(end));
        editor.CaretOffset = start + snippet.Sql.Length;
        editor.Selection = new(editor.CaretOffset, 0);
        editor.FocusEditor();
    }

    [RelayCommand]
    private async Task DeleteAsync(Snippet? snippet, CancellationToken ct)
    {
        if (snippet is null || !await dialogs.ConfirmAsync("Delete snippet", $"Delete the snippet '{snippet.Name}'?"))
            return;
        await store.DeleteAsync(snippet.Id, ct);
        Snippets.Remove(snippet);
    }

    [RelayCommand]
    private async Task RenameAsync(Snippet? snippet, CancellationToken ct)
    {
        if (snippet is null)
            return;
        var name = await dialogs.PromptAsync("Rename snippet", "Snippet name", snippet.Name);
        if (string.IsNullOrWhiteSpace(name) || name.Trim() == snippet.Name)
            return;
        await store.SaveAsync(snippet with { Name = name.Trim() }, ct);
        await LoadAsync(ct);
    }
}
