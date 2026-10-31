using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Squill.Presentation;
using Squill.Presentation.Query;

namespace Squill.App.Avalonia.Services;

/// <summary>Modal dialogs built from plain Avalonia windows, owned by the main window.</summary>
internal sealed class AvaloniaDialogService : IDialogService
{
    private static Window? Owner =>
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

    /// <inheritdoc />
    public async ValueTask<bool> ConfirmAsync(string title, string message) =>
        await ShowAsync(title, message, null, ("OK", true), ("Cancel", false)) is true;

    /// <inheritdoc />
    public async ValueTask<string?> PromptAsync(string title, string message, string? initial)
    {
        var box = new TextBox { Text = initial };
        return await ShowAsync(title, message, box, ("OK", true), ("Cancel", false)) is true ? box.Text ?? "" : null;
    }

    /// <inheritdoc />
    public async ValueTask<string?> PickSaveFileAsync(string suggestedName, string extension)
    {
        var storage = Owner?.StorageProvider;
        if (storage is null)
            return null;
        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = suggestedName,
            DefaultExtension = extension,
            FileTypeChoices = [new FilePickerFileType(extension.ToUpperInvariant()) { Patterns = [$"*.{extension}"] }],
        });
        return file?.TryGetLocalPath();
    }

    /// <inheritdoc />
    public async ValueTask ShowErrorAsync(string title, string message) =>
        await ShowAsync(title, message, null, ("OK", true));

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<ParameterValue>?> PromptParametersAsync(
        IReadOnlyList<string> names,
        IReadOnlyDictionary<string, ParameterValue> defaults)
    {
        var panel = new StackPanel { Spacing = 6 };
        var inputs = new List<(string Name, TextBox Box, CheckBox IsNull)>();
        foreach (var name in names)
        {
            defaults.TryGetValue(name, out var previous);
            var box = new TextBox { Text = previous?.Text, PlaceholderText = name };
            var isNull = new CheckBox { Content = "NULL", IsChecked = previous?.IsNull ?? false };
            panel.Children.Add(new TextBlock { Text = name });
            panel.Children.Add(box);
            panel.Children.Add(isNull);
            inputs.Add((name, box, isNull));
        }

        if (await ShowAsync("Parameters", "Enter the parameter values.", panel, ("OK", true), ("Cancel", false)) is not true)
            return null;
        return [.. inputs.Select(i => new ParameterValue(i.Name, i.Box.Text, i.IsNull.IsChecked == true))];
    }

    private static async Task<bool?> ShowAsync(string title, string message, Control? content, params (string Text, bool Result)[] buttons)
    {
        var window = new Window
        {
            Title = title,
            Width = 420,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        foreach (var (text, result) in buttons)
        {
            var button = new Button { Content = text };
            button.Click += (_, _) => window.Close(result);
            row.Children.Add(button);
        }

        var body = new StackPanel { Margin = new Thickness(16), Spacing = 12 };
        body.Children.Add(new TextBlock { Text = message, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap });
        if (content is not null)
            body.Children.Add(content);
        body.Children.Add(row);
        window.Content = body;

        return Owner is { } owner ? await window.ShowDialog<bool?>(owner) : null;
    }
}
