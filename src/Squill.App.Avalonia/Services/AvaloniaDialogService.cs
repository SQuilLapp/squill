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
    // The window in front owns the dialog: with the connection editor open (modal over the main window), a dialog it raises must not open under it.
    private static Window? Owner =>
        Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.Windows.LastOrDefault(w => w.IsVisible && w.IsActive) ?? desktop.MainWindow
            : null;

    /// <inheritdoc />
    public async ValueTask<bool> ConfirmAsync(string title, string message) =>
        await ShowAsync(title, message, null, (UiText.Get("dialog.ok"), true), (UiText.Get("dialog.cancel"), false)) is true;

    /// <inheritdoc />
    public async ValueTask<bool> ConfirmSshHostKeyAsync(Squill.Platform.Ssh.SshHostKeyRequest request) =>
        Owner is { } owner && await Views.SshHostKeyWindow.ShowAsync(owner, request);

    /// <inheritdoc />
    public async ValueTask<Squill.Presentation.Safety.ProductionConfirmationResult> ConfirmProductionAsync(Squill.Presentation.Safety.ProductionConfirmationRequest request) =>
        Owner is { } owner ? await Views.ProductionConfirmWindow.ShowAsync(owner, request) : new(false, false);

    /// <inheritdoc />
    public async ValueTask<Squill.Presentation.Safety.TransactionChoice> AskTransactionAsync(Squill.Presentation.Safety.TransactionCloseRequest request) =>
        Owner is { } owner ? await Views.TransactionAskWindow.ShowAsync(owner, request) : Squill.Presentation.Safety.TransactionChoice.Cancel;

    /// <inheritdoc />
    public async ValueTask<string?> PromptAsync(string title, string message, string? initial)
    {
        var box = new TextBox { Text = initial };
        return await ShowAsync(title, message, box, (UiText.Get("dialog.ok"), true), (UiText.Get("dialog.cancel"), false)) is true ? box.Text ?? "" : null;
    }

    /// <inheritdoc />
    public async ValueTask<string?> PromptPasswordAsync(string profileName) =>
        Owner is { } owner ? await Views.PasswordPromptWindow.ShowAsync(owner, profileName) : null;

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
    public async ValueTask<string?> PickOpenFileAsync(string title, IReadOnlyList<string> extensions)
    {
        var storage = Owner?.StorageProvider;
        if (storage is null)
            return null;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType(title) { Patterns = [.. extensions.Select(e => $"*.{e}")] }],
        });
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    /// <inheritdoc />
    public async ValueTask ShowImportWizardAsync(Squill.Presentation.Import.ImportWizardViewModel wizard)
    {
        if (Owner is { } owner)
            await Views.ImportWizardWindow.ShowAsync(owner, wizard);
    }

    /// <inheritdoc />
    public async ValueTask ShowErrorAsync(string title, string message) =>
        await ShowAsync(title, message, null, (UiText.Get("dialog.ok"), true));

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
