using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Squill.Platform.Storage;
using Squill.Presentation.Panels;

namespace Squill.App.WinUI.Views;

/// <summary>The saved snippets with save, insert, rename and delete.</summary>
public sealed partial class SnippetsView : UserControl
{
    private SnippetsViewModel? _snippets;

    /// <summary>Creates the view.</summary>
    public SnippetsView() => InitializeComponent();

    /// <summary>Connects the view to the snippets it shows.</summary>
    /// <param name="snippets">The snippets view-model.</param>
    public void Attach(SnippetsViewModel snippets)
    {
        _snippets = snippets;
        List.ItemsSource = snippets.Snippets;
    }

    /// <summary>Reloads the snippets from the store.</summary>
    public void Reload() => _snippets!.LoadCommand.Execute(null);

    private Snippet? Selected => List.SelectedItem as Snippet;

    private void OnSaveClicked(object sender, RoutedEventArgs e) => _snippets!.SaveCurrentCommand.Execute(null);

    private void OnDoubleTapped(object sender, DoubleTappedRoutedEventArgs e) => _snippets!.InsertCommand.Execute(Selected);

    private void OnInsertClicked(object sender, RoutedEventArgs e) => _snippets!.InsertCommand.Execute(Selected);

    private void OnRenameClicked(object sender, RoutedEventArgs e) => _snippets!.RenameCommand.Execute(Selected);

    private void OnDeleteClicked(object sender, RoutedEventArgs e) => _snippets!.DeleteCommand.Execute(Selected);
}
