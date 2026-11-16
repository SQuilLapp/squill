using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Squill.Platform.Ssh;
using Squill.Presentation.Localization;

namespace Squill.App.Avalonia.Views;

/// <summary>The question asked the first time an SSH server is met: trust its host key or cancel. Annuler is the default, which Enter, Escape and closing the window choose.</summary>
internal sealed class SshHostKeyWindow : Window
{
    private bool _trusted;

    /// <summary>Creates the window for a request.</summary>
    /// <param name="request">The server and the key it presented.</param>
    public SshHostKeyWindow(SshHostKeyRequest request)
    {
        Title = UiText.Get("ssh.hostkey.title");
        Width = 520;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var mono = this.TryFindResource("SquillFontMono", ActualThemeVariant, out var font) && font is FontFamily family ? family : FontFamily.Default;
        IBrush? Brush(string key) => this.TryFindResource(key, ActualThemeVariant, out var value) ? value as IBrush : null;
        Background = Brush("SquillBgRaisedBrush");

        var body = new StackPanel { Spacing = 12, Margin = new Thickness(24, 20, 24, 8) };
        body.Children.Add(new TextBlock { Text = UiText.Get("ssh.hostkey.title"), FontSize = 20, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap, Foreground = Brush("SquillFgBrush") });
        body.Children.Add(new TextBlock { Text = UiText.Format("ssh.hostkey.body", request.Host, request.Port, request.Algorithm), TextWrapping = TextWrapping.Wrap, Foreground = Brush("SquillFg2Brush") });
        var fingerprint = new TextBox { IsReadOnly = true, FontFamily = mono, FontSize = 12, TextWrapping = TextWrapping.Wrap, Background = Brush("SquillBgBaseBrush"), Foreground = Brush("SquillFgBrush") };
        fingerprint.Text = request.Fingerprint;
        body.Children.Add(fingerprint);

        // Trust is neither the default nor the cancel button: it only reacts to a click, and Enter anywhere in the window is Annuler.
        Trust = new Button { Content = UiText.Get("ssh.hostkey.trust"), Height = 30, MinHeight = 30, Classes = { "primary" } };
        Cancel = new Button { Content = UiText.Get("ssh.hostkey.cancel"), Height = 30, MinHeight = 30, IsDefault = true, IsCancel = true };
        Trust.Click += (_, _) =>
        {
            _trusted = true;
            Close();
        };
        Cancel.Click += (_, _) => Close();
        var footer = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(24, 12, 24, 20), Children = { Trust, Cancel } };

        // Tunnel: the read-only box and every button see the key after this, so Enter never trusts.
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        Opened += (_, _) => Cancel.Focus();
        Content = new StackPanel { Children = { body, footer } };
    }

    /// <summary>Gets the button that remembers the key; reacts to a click only.</summary>
    public Button Trust { get; }

    /// <summary>Gets the Annuler button, the default and cancel one.</summary>
    public Button Cancel { get; }

    /// <summary>Gets a value indicating whether the user clicked the trust button.</summary>
    public bool Trusted => _trusted;

    /// <summary>Shows the window over another one.</summary>
    /// <param name="owner">The window that owns the dialog.</param>
    /// <param name="request">The server and the key it presented.</param>
    /// <returns><see langword="true"/> only when the user clicked the trust button.</returns>
    public static async Task<bool> ShowAsync(Window owner, SshHostKeyRequest request)
    {
        var window = new SshHostKeyWindow(request);
        await window.ShowDialog(owner);
        return window.Trusted;
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Escape))
            return;
        e.Handled = true;
        Close();
    }
}
