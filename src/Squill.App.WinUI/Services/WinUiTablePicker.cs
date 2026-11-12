using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Squill.Presentation.Explorer;
using Squill.Presentation.Localization;

namespace Squill.App.WinUI.Services;

/// <summary>The dialog in which the user checks the tables and views the explorer shows, built from a <see cref="ContentDialog"/>.</summary>
/// <param name="root">Gives the element whose <see cref="XamlRoot"/> hosts the dialog, or <see langword="null"/> when the window is not ready.</param>
internal sealed class WinUiTablePicker(Func<XamlRoot?> root) : ITableVisibilityPicker
{
    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<string>?> PickVisibleAsync(string title, IReadOnlyList<TableChoice> choices)
    {
        if (root() is not { } xamlRoot)
            return null;
        var boxes = choices.Select(c => new CheckBox { Content = c.Key, IsChecked = c.Visible, Tag = c.Key, MinHeight = 28 }).ToList();
        var list = new ListView { ItemsSource = boxes, SelectionMode = ListViewSelectionMode.None, Height = 320 };
        var filter = new TextBox { PlaceholderText = UiText.Get("explorer.filter") };
        var all = new Button { Content = UiText.Get("explorer.selectAll") };
        var none = new Button { Content = UiText.Get("explorer.selectNone") };

        List<CheckBox> Shown() => filter.Text.Length == 0
            ? boxes
            : boxes.Where(b => ((string)b.Tag).Contains(filter.Text, StringComparison.OrdinalIgnoreCase)).ToList();

        filter.TextChanged += (_, _) => list.ItemsSource = Shown();
        all.Click += (_, _) => Shown().ForEach(b => b.IsChecked = true);
        none.Click += (_, _) => Shown().ForEach(b => b.IsChecked = false);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        buttons.Children.Add(all);
        buttons.Children.Add(none);
        var body = new StackPanel { Spacing = 8, MinWidth = 420 };
        body.Children.Add(filter);
        body.Children.Add(buttons);
        body.Children.Add(list);
        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = UiText.Format("explorer.tablesDialog", title),
            Content = body,
            PrimaryButtonText = UiText.Get("dialog.ok"),
            CloseButtonText = UiText.Get("dialog.cancel"),
            DefaultButton = ContentDialogButton.Primary,
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary
            ? boxes.Where(b => b.IsChecked == true).Select(b => (string)b.Tag).ToArray()
            : null;
    }
}
