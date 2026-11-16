using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Squill.Platform.Ssh;
using Squill.Presentation.Localization;

namespace Squill.App.WinUI.Views;

/// <summary>The question asked the first time an SSH server is met: trust its host key or cancel. <c>Annuler</c> is the default, which <c>Entrée</c>, <c>Échap</c> and closing the window choose.</summary>
internal static class SshHostKeyDialog
{
    /// <summary>Shows the question over a window.</summary>
    /// <param name="root">The content of the window that owns the dialog.</param>
    /// <param name="request">The server and the key it presented.</param>
    /// <returns><see langword="true"/> only when the user clicked the trust button.</returns>
    public static async Task<bool> ShowAsync(FrameworkElement root, SshHostKeyRequest request)
    {
        Microsoft.UI.Xaml.Media.Brush Brush(string key) => Tokens.Brush(root, key);
        var mono = (Microsoft.UI.Xaml.Media.FontFamily)Application.Current.Resources["SquillFontMono"];
        var trusted = false;
        var dialog = new ContentDialog { XamlRoot = root.XamlRoot, RequestedTheme = root.ActualTheme, Padding = new Thickness(0), Background = Brush("SquillBgRaisedBrush") };
        dialog.Resources["ContentDialogMaxWidth"] = 520.0;
        dialog.Resources["ContentDialogMinWidth"] = 520.0;

        var body = new StackPanel { Spacing = 12, Padding = new Thickness(24, 20, 24, 8) };
        body.Children.Add(new TextBlock { Text = UiText.Get("ssh.hostkey.title"), FontSize = 20, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Foreground = Brush("SquillFgBrush") });
        body.Children.Add(new TextBlock { Text = UiText.Format("ssh.hostkey.body", request.Host, request.Port, request.Algorithm), TextWrapping = TextWrapping.Wrap, Foreground = Brush("SquillFg2Brush") });
        body.Children.Add(new TextBox
        {
            Text = request.Fingerprint,
            IsReadOnly = true,
            FontFamily = mono,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brush("SquillFgBrush"),
            Background = Brush("SquillBgBaseBrush"),
        });

        var trust = new Button { Content = UiText.Get("ssh.hostkey.trust"), Height = 30, MinHeight = 30, Style = (Style)Application.Current.Resources["PrimaryButtonStyle"] };
        trust.Click += (_, _) =>
        {
            trusted = true;
            dialog.Hide();
        };
        var cancel = new Button { Content = UiText.Get("ssh.hostkey.cancel"), Height = 30, MinHeight = 30 };
        cancel.Click += (_, _) => dialog.Hide();
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Padding = new Thickness(24, 12, 24, 20), Children = { trust, cancel } };

        // Annuler is the default: Entrée cancels whatever has the focus, and only a click trusts.
        dialog.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                e.Handled = true;
                dialog.Hide();
            }
        };
        dialog.Opened += (_, _) => root.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () => cancel.Focus(FocusState.Keyboard));
        dialog.Content = new StackPanel { Children = { body, buttons } };
        await dialog.ShowAsync();
        return trusted;
    }
}
