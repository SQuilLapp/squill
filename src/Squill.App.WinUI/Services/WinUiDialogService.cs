using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Squill.Presentation;
using Squill.Presentation.Localization;
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
        await ShowAsync(title, message, null, UiText.Get("dialog.ok"), UiText.Get("dialog.cancel")) == ContentDialogResult.Primary;

    /// <inheritdoc />
    public async ValueTask<Squill.Presentation.Safety.ProductionConfirmationResult> ConfirmProductionAsync(Squill.Presentation.Safety.ProductionConfirmationRequest request) =>
        _window?.Content is FrameworkElement { XamlRoot: not null } root
            ? await Views.ProductionConfirmDialog.ShowAsync(root, request)
            : new(false, false);

    /// <inheritdoc />
    public async ValueTask<Squill.Presentation.Safety.TransactionChoice> AskTransactionAsync(Squill.Presentation.Safety.TransactionCloseRequest request) =>
        _window?.Content is FrameworkElement { XamlRoot: not null } root
            ? await Views.TransactionAskDialog.ShowAsync(root, request)
            : Squill.Presentation.Safety.TransactionChoice.Cancel;

    /// <inheritdoc />
    public async ValueTask<string?> PromptAsync(string title, string message, string? initial)
    {
        var box = new TextBox { Text = initial ?? "" };
        return await ShowAsync(title, message, box, UiText.Get("dialog.ok"), UiText.Get("dialog.cancel")) == ContentDialogResult.Primary ? box.Text : null;
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
        await ShowAsync(title, message, null, UiText.Get("dialog.ok"), null);

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<ParameterValue>?> PromptParametersAsync(
        IReadOnlyList<string> names,
        IReadOnlyDictionary<string, ParameterValue> defaults)
    {
        var rows = new List<ParameterPromptRow>();
        var panel = new StackPanel { Spacing = 8, MinWidth = 440 };
        panel.Children.Add(new TextBlock { Text = UiText.Format("editor.parametersDetected", names.Count), Style = (Style)Application.Current.Resources["CapsTextBlockStyle"] });
        var dialog = NewDialog(UiText.Get("editor.parameters"), panel, UiText.Get("editor.runWithValues"), UiText.Get("editor.ignore"));
        if (dialog is null)
            return null;
        dialog.PrimaryButtonStyle = (Style)Application.Current.Resources["PrimaryButtonStyle"];
        void Refresh() => dialog.IsPrimaryButtonEnabled = ParameterPromptRow.AllValid(rows);
        foreach (var name in names)
        {
            defaults.TryGetValue(name, out var previous);
            var row = new ParameterPromptRow(name) { Text = previous?.Text, IsNull = previous?.IsNull ?? false };
            rows.Add(row);
            var box = new TextBox { Text = row.Text ?? "", FontFamily = (Microsoft.UI.Xaml.Media.FontFamily)Application.Current.Resources["SquillFontMono"], IsEnabled = !row.IsNull };
            var isNull = new CheckBox { Content = UiText.Get("editor.passNull"), IsChecked = row.IsNull };
            var error = new TextBlock { FontSize = 12, Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SquillDangerBrush"] };
            void Sync()
            {
                error.Text = row.Error ?? "";
                error.Visibility = row.Error is null ? Visibility.Collapsed : Visibility.Visible;
                box.IsEnabled = !row.IsNull;
                Refresh();
            }

            box.TextChanged += (_, _) => { row.Text = box.Text; Sync(); };
            isNull.Click += (_, _) => { row.IsNull = isNull.IsChecked == true; Sync(); };
            var line = new Grid { ColumnSpacing = 8, ColumnDefinitions = { new ColumnDefinition { Width = new GridLength(120) }, new ColumnDefinition(), new ColumnDefinition { Width = GridLength.Auto } } };
            var label = new TextBlock { Text = name, VerticalAlignment = VerticalAlignment.Center, Style = (Style)Application.Current.Resources["MonoTextBlockStyle"] };
            Grid.SetColumn(box, 1);
            Grid.SetColumn(isNull, 2);
            line.Children.Add(label);
            line.Children.Add(box);
            line.Children.Add(isNull);
            panel.Children.Add(line);
            panel.Children.Add(error);
            Sync();
        }

        Refresh();
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            return null;
        return rows.Select(r => new ParameterValue(r.Name, r.Text, r.IsNull)).ToArray();
    }

    private ContentDialog? NewDialog(string title, UIElement content, string primary, string close) =>
        _window?.Content?.XamlRoot is not { } root
            ? null
            : new ContentDialog { XamlRoot = root, Title = title, Content = content, PrimaryButtonText = primary, CloseButtonText = close, DefaultButton = ContentDialogButton.Primary };

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
