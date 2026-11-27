using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Squill.Presentation.Localization;

namespace Squill.App.Avalonia.Views;

/// <summary>Asks the database password of a connection that is set to "ask on connect": a masked box, Enter connects, Escape cancels. The text is only returned to the caller, never kept or logged.</summary>
internal sealed class PasswordPromptWindow : Window
{
    private string? _password;

    /// <summary>Creates the window for a connection.</summary>
    /// <param name="profileName">Name of the connection, shown in the prompt.</param>
    public PasswordPromptWindow(string profileName)
    {
        Title = UiText.Get("password.prompt.title");
        Width = 460;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        IBrush? Brush(string key) => this.TryFindResource(key, ActualThemeVariant, out var value) ? value as IBrush : null;
        Background = Brush("SquillBgRaisedBrush");

        Box = new TextBox { PasswordChar = '●' };
        Connect = new Button { Content = UiText.Get("password.prompt.connect"), Height = 30, MinHeight = 30, Classes = { "primary" } };
        Cancel = new Button { Content = UiText.Get("dialog.cancel"), Height = 30, MinHeight = 30 };
        Connect.Click += (_, _) => Accept();
        Cancel.Click += (_, _) => Close();

        var body = new StackPanel { Spacing = 12, Margin = new Thickness(24, 20, 24, 20) };
        body.Children.Add(new TextBlock { Text = UiText.Get("password.prompt.title"), FontSize = 20, FontWeight = FontWeight.SemiBold, Foreground = Brush("SquillFgBrush") });
        body.Children.Add(new TextBlock { Text = UiText.Format("password.prompt", profileName), TextWrapping = TextWrapping.Wrap, Foreground = Brush("SquillFg2Brush") });
        body.Children.Add(Box);
        body.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { Connect, Cancel } });

        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        Opened += (_, _) => Box.Focus();
        Content = body;
    }

    /// <summary>Gets the masked password box.</summary>
    public TextBox Box { get; }

    /// <summary>Gets the button that takes the typed password.</summary>
    public Button Connect { get; }

    /// <summary>Gets the button that cancels the prompt.</summary>
    public Button Cancel { get; }

    /// <summary>Gets the typed password, or <see langword="null"/> when the prompt was cancelled.</summary>
    public string? Password => _password;

    /// <summary>Shows the window over another one.</summary>
    /// <param name="owner">The window that owns the dialog.</param>
    /// <param name="profileName">Name of the connection, shown in the prompt.</param>
    /// <returns>The typed password, or <see langword="null"/> when the user cancelled.</returns>
    public static async Task<string?> ShowAsync(Window owner, string profileName)
    {
        var window = new PasswordPromptWindow(profileName);
        await window.ShowDialog(owner);
        return window.Password;
    }

    private void Accept()
    {
        _password = Box.Text ?? "";
        Close();
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !Cancel.IsFocused)
            Accept();
        else if (e.Key is Key.Escape or Key.Enter)
            Close();
        else
            return;
        e.Handled = true;
    }
}
