using Avalonia.Controls;
using Avalonia.Input;
using Squill.Platform.Storage;
using Squill.Presentation.Panels;

namespace Squill.App.Avalonia.Views;

/// <summary>The saved snippets; a double-click inserts one into the active editor.</summary>
public sealed partial class SnippetsView : UserControl
{
    /// <summary>Creates the view.</summary>
    public SnippetsView() => InitializeComponent();

    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is SnippetsViewModel snippets && List.SelectedItem is Snippet snippet)
            snippets.InsertCommand.Execute(snippet);
    }
}
