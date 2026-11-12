using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Squill.Presentation;
using Squill.Presentation.Localization;
using Squill.Presentation.Query;

namespace Squill.App.Avalonia.Services;

/// <summary>Modal dialogs built from plain Avalonia windows, owned by the main window.</summary>
internal sealed class AvaloniaDialogService : IDialogService
{
    private static Window? Owner =>
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

    /// <inheritdoc />
    public async ValueTask<bool> ConfirmAsync(string title, string message) =>
        await ShowAsync(title, message, null, (UiText.Get("dialog.ok"), true), (UiText.Get("dialog.cancel"), false)) is true;

    /// <inheritdoc />
    public async ValueTask<Squill.Presentation.Safety.ProductionConfirmationResult> ConfirmProductionAsync(Squill.Presentation.Safety.ProductionConfirmationRequest request) =>
        new(await ConfirmAsync(request.Title, request.Message), false); // temporary until the M15 dialog (P15-16 / P15-18)

    /// <inheritdoc />
    public async ValueTask<string?> PromptAsync(string title, string message, string? initial)
    {
        var box = new TextBox { Text = initial };
        return await ShowAsync(title, message, box, (UiText.Get("dialog.ok"), true), (UiText.Get("dialog.cancel"), false)) is true ? box.Text ?? "" : null;
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
        await ShowAsync(title, message, null, (UiText.Get("dialog.ok"), true));

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<ParameterValue>?> PromptParametersAsync(
        IReadOnlyList<string> names,
        IReadOnlyDictionary<string, ParameterValue> defaults)
    {
        var rows = new List<ParameterPromptRow>();
        var panel = new StackPanel { Spacing = 8 };
        var run = new Button { Content = UiText.Get("editor.runWithValues"), Classes = { "primary" } };
        void Refresh() => run.IsEnabled = ParameterPromptRow.AllValid(rows);
        panel.Children.Add(new TextBlock { Text = UiText.Format("editor.parametersDetected", names.Count), Classes = { "caps" } });
        foreach (var name in names)
        {
            defaults.TryGetValue(name, out var previous);
            var row = new ParameterPromptRow(name) { Text = previous?.Text, IsNull = previous?.IsNull ?? false };
            rows.Add(row);
            var box = new TextBox { Text = row.Text, Classes = { "mono" }, IsEnabled = !row.IsNull };
            var isNull = new CheckBox { Content = UiText.Get("editor.passNull"), IsChecked = row.IsNull };
            var error = new TextBlock { FontSize = 12, IsVisible = row.Error is not null, Text = row.Error };
            error.Bind(TextBlock.ForegroundProperty, error.GetResourceObservable("SquillDangerBrush"));
            void Sync()
            {
                error.Text = row.Error;
                error.IsVisible = row.Error is not null;
                box.IsEnabled = !row.IsNull;
                Refresh();
            }

            box.TextChanged += (_, _) => { row.Text = box.Text; Sync(); };
            isNull.IsCheckedChanged += (_, _) => { row.IsNull = isNull.IsChecked == true; Sync(); };
            var line = new Grid { ColumnDefinitions = new ColumnDefinitions("120,*,Auto"), ColumnSpacing = 8 };
            var label = new TextBlock { Text = name, VerticalAlignment = VerticalAlignment.Center, Classes = { "mono" } };
            Grid.SetColumn(box, 1);
            Grid.SetColumn(isNull, 2);
            line.Children.Add(label);
            line.Children.Add(box);
            line.Children.Add(isNull);
            panel.Children.Add(line);
            panel.Children.Add(error);
        }

        Refresh();
        var ok = await ShowAsync(UiText.Get("editor.parameters"), null, panel, run, true, (UiText.Get("editor.ignore"), false)) is true;
        return ok ? [.. rows.Select(r => new ParameterValue(r.Name, r.Text, r.IsNull))] : null;
    }

    private static Task<bool?> ShowAsync(string title, string message, Control? content, params (string Text, bool Result)[] buttons) =>
        ShowAsync(title, message, content, null, false, buttons);

    private static async Task<bool?> ShowAsync(string title, string? message, Control? content, Button? primary, bool primaryResult, params (string Text, bool Result)[] buttons)
    {
        var window = new Window
        {
            Title = title,
            Width = 520,
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

        if (primary is not null)
        {
            primary.Click += (_, _) => window.Close(primaryResult);
            row.Children.Add(primary);
        }

        var body = new StackPanel { Margin = new Thickness(16), Spacing = 12 };
        if (message is not null)
            body.Children.Add(new TextBlock { Text = message, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap });
        if (content is not null)
            body.Children.Add(content);
        body.Children.Add(row);
        window.Content = body;

        return Owner is { } owner ? await window.ShowDialog<bool?>(owner) : null;
    }
}
