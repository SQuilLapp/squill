using Avalonia.Controls;
using Avalonia.Layout;
using Squill.Presentation.Explorer;
using Squill.Presentation.Localization;

namespace Squill.App.Avalonia.Services;

/// <summary>The window in which the user checks the tables and views the explorer shows.</summary>
/// <param name="owner">Gives the window that owns the dialog, or <see langword="null"/> when none is open.</param>
internal sealed class AvaloniaTablePicker(Func<Window?> owner) : ITableVisibilityPicker
{
    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<string>?> PickVisibleAsync(string title, IReadOnlyList<TableChoice> choices)
    {
        if (owner() is not { } parent)
            return null;
        var boxes = choices.Select(c => new CheckBox { Content = c.Key, IsChecked = c.Visible, Tag = c.Key }).ToList();
        var list = new ListBox { ItemsSource = boxes, Height = 320 };
        var filter = new TextBox { PlaceholderText = UiText.Get("explorer.filter") };
        var all = new Button { Content = UiText.Get("explorer.selectAll") };
        var none = new Button { Content = UiText.Get("explorer.selectNone") };
        var ok = new Button { Content = UiText.Get("dialog.ok"), Classes = { "primary" } };
        var cancel = new Button { Content = UiText.Get("dialog.cancel") };

        List<CheckBox> Shown() => string.IsNullOrEmpty(filter.Text)
            ? boxes
            : boxes.Where(b => ((string)b.Tag!).Contains(filter.Text, StringComparison.OrdinalIgnoreCase)).ToList();

        filter.TextChanged += (_, _) => list.ItemsSource = Shown();
        all.Click += (_, _) => Shown().ForEach(b => b.IsChecked = true);
        none.Click += (_, _) => Shown().ForEach(b => b.IsChecked = false);

        var window = new Window
        {
            Title = UiText.Format("explorer.tablesDialog", title),
            Width = 480,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        ok.Click += (_, _) => window.Close(true);
        cancel.Click += (_, _) => window.Close(false);
        var tools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        tools.Children.Add(all);
        tools.Children.Add(none);
        var footer = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        footer.Children.Add(cancel);
        footer.Children.Add(ok);
        var body = new StackPanel { Margin = new global::Avalonia.Thickness(16), Spacing = 10 };
        body.Children.Add(filter);
        body.Children.Add(tools);
        body.Children.Add(list);
        body.Children.Add(footer);
        window.Content = body;

        return await window.ShowDialog<bool?>(parent) is true
            ? [.. boxes.Where(b => b.IsChecked == true).Select(b => (string)b.Tag!)]
            : null;
    }
}
