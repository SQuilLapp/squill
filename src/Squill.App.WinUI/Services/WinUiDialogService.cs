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
    private static readonly SemaphoreSlim Gate = new(1, 1); // WinUI shows one ContentDialog at a time, so a second request waits here
    private Window? _window;

    /// <summary>Sets the window whose content hosts the dialogs.</summary>
    /// <param name="window">The main window.</param>
    public void Attach(Window window) => _window = window;

    /// <inheritdoc />
    public async ValueTask<bool> ConfirmAsync(string title, string message) =>
        await ShowAsync(title, message, null, UiText.Get("dialog.ok"), UiText.Get("dialog.cancel")) == ContentDialogResult.Primary;

    /// <inheritdoc />
    public async ValueTask<bool> ConfirmSshHostKeyAsync(Squill.Platform.Ssh.SshHostKeyRequest request) =>
        _window?.Content is FrameworkElement { XamlRoot: not null } root && await Views.SshHostKeyDialog.ShowAsync(root, request);

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
    public async ValueTask<string?> PromptPasswordAsync(string profileName)
    {
        // Masked box; Enter takes the default (primary) button, Escape closes the dialog without a password.
        var box = new PasswordBox { MinHeight = 30 };
        var result = await ShowAsync(UiText.Get("password.prompt.title"), UiText.Format("password.prompt", profileName), box, UiText.Get("password.prompt.connect"), UiText.Get("dialog.cancel"), box);
        return result == ContentDialogResult.Primary ? box.Password : null;
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
    public async ValueTask<string?> PickOpenFileAsync(string title, IReadOnlyList<string> extensions)
    {
        if (_window is null)
            return null;
        var picker = new FileOpenPicker();
        foreach (var extension in extensions)
            picker.FileTypeFilter.Add($".{extension}");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(_window));
        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }

    /// <inheritdoc />
    public async ValueTask ShowImportWizardAsync(Squill.Presentation.Import.ImportWizardViewModel wizard)
    {
        if (_window is null)
            return;
        var window = new Views.ImportWizardWindow(_window, wizard);
        window.Activate();
        await window.Closure;
    }

    /// <inheritdoc />
    public async ValueTask ShowErrorAsync(string title, string message) =>
        await ShowAsync(title, message, null, UiText.Get("dialog.ok"), null);

    private ContentDialog? NewDialog(string title, UIElement content, string primary, string close) =>
        _window?.Content?.XamlRoot is not { } root
            ? null
            : new ContentDialog { XamlRoot = root, Title = title, Content = content, PrimaryButtonText = primary, CloseButtonText = close, DefaultButton = ContentDialogButton.Primary };

    private async Task<ContentDialogResult> ShowAsync(string title, string message, UIElement? input, string primary, string? close, Control? focus = null)
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
            RequestedTheme = ((FrameworkElement)_window.Content).ActualTheme, // the popup layer does not inherit the app theme
        };
        if (focus is not null)
            dialog.Opened += (_, _) => focus.Focus(FocusState.Programmatic);
        await Gate.WaitAsync();
        try
        {
            return await dialog.ShowAsync();
        }
        finally
        {
            Gate.Release();
        }
    }
}
