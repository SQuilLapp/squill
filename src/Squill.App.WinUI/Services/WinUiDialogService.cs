using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Squill.Presentation;
using Squill.Presentation.Query;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Squill.App.WinUI.Services;

/// <summary>Modal dialogs built from <see cref="ContentDialog"/>, shown over the main window.</summary>
internal sealed class WinUiDialogService : IDialogService
{
    private Window? _window;

    /// <summary>Sets the window whose content hosts the dialogs.</summary>
    /// <param name="window">The main window.</param>
    public void Attach(Window window) => _window = window;

    /// <inheritdoc />
    public async ValueTask<bool> ConfirmAsync(string title, string message) =>
        await ShowAsync(title, message, null, "OK", "Cancel") == ContentDialogResult.Primary;

    /// <inheritdoc />
    public async ValueTask<string?> PromptAsync(string title, string message, string? initial)
    {
        var box = new TextBox { Text = initial ?? "" };
        return await ShowAsync(title, message, box, "OK", "Cancel") == ContentDialogResult.Primary ? box.Text : null;
    }

    /// <inheritdoc />
    public async ValueTask<string?> PickSaveFileAsync(string suggestedName, string extension)
    {
        if (_window is null)
            return null;
        var picker = new FileSavePicker { SuggestedFileName = suggestedName };
        picker.FileTypeChoices.Add(extension.ToUpperInvariant(), [$".{extension}"]);
        // An unpackaged app must tell the picker which window owns it.
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(_window));
        var file = await picker.PickSaveFileAsync();
        return file?.Path;
    }

    /// <inheritdoc />
    public async ValueTask ShowErrorAsync(string title, string message) =>
        await ShowAsync(title, message, null, "OK", null);

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
            var box = new TextBox { Text = previous?.Text ?? "", PlaceholderText = name };
            var isNull = new CheckBox { Content = "NULL", IsChecked = previous?.IsNull ?? false };
            panel.Children.Add(new TextBlock { Text = name });
            panel.Children.Add(box);
            panel.Children.Add(isNull);
            inputs.Add((name, box, isNull));
        }

        if (await ShowAsync("Parameters", "Enter the parameter values.", panel, "OK", "Cancel") != ContentDialogResult.Primary)
            return null;
        return inputs.Select(i => new ParameterValue(i.Name, i.Box.Text, i.IsNull.IsChecked == true)).ToArray();
    }

    private async Task<ContentDialogResult> ShowAsync(string title, string message, UIElement? input, string primary, string? close)
    {
        if (_window?.Content?.XamlRoot is not { } root)
            return ContentDialogResult.None;
        var body = new StackPanel { Spacing = 12 };
        body.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
        if (input is not null)
            body.Children.Add(input);
        var dialog = new ContentDialog
        {
            XamlRoot = root,
            Title = title,
            Content = body,
            PrimaryButtonText = primary,
            CloseButtonText = close ?? "",
            DefaultButton = ContentDialogButton.Primary,
        };
        return await dialog.ShowAsync();
    }
}
