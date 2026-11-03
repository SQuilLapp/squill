using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Squill.App.WinUI.Views;

/// <summary>Paints the native caption bar of a window from the design tokens of the theme its content is shown in.</summary>
internal static class CaptionBar
{
    /// <summary>Colors the caption buttons and, unless the content is extended into the title bar, the bar itself.</summary>
    /// <param name="titleBar">The title bar of the window.</param>
    /// <param name="root">The root element of the window content, whose actual theme decides the variant.</param>
    /// <param name="extended">Whether the content extends into the title bar, which then stays transparent.</param>
    public static void Paint(AppWindowTitleBar titleBar, FrameworkElement root, bool extended)
    {
        var bar = extended ? Colors.Transparent : Tokens.Color(root, "SquillBgBaseBrush");
        titleBar.BackgroundColor = bar;
        titleBar.InactiveBackgroundColor = bar;
        titleBar.ForegroundColor = Tokens.Color(root, "SquillFgBrush");
        titleBar.InactiveForegroundColor = Tokens.Color(root, "SquillFgDisabledBrush");
        titleBar.ButtonBackgroundColor = bar;
        titleBar.ButtonInactiveBackgroundColor = bar;
        titleBar.ButtonForegroundColor = Tokens.Color(root, "SquillFgBrush");
        titleBar.ButtonInactiveForegroundColor = Tokens.Color(root, "SquillFgDisabledBrush");
        titleBar.ButtonHoverBackgroundColor = Tokens.Color(root, "SquillBgHoverBrush");
        titleBar.ButtonHoverForegroundColor = Tokens.Color(root, "SquillFgBrush");
        titleBar.ButtonPressedBackgroundColor = Tokens.Color(root, "SquillBgActiveBrush");
        titleBar.ButtonPressedForegroundColor = Tokens.Color(root, "SquillFgBrush");
    }

    /// <summary>
    /// Replaces the native caption bar of a secondary window by a drawn one like the main window's: the system bar leaves a
    /// light 1 px line between itself and the content in the dark theme.
    /// </summary>
    /// <param name="window">The secondary window; its <see cref="Window.Title"/> is shown on the bar.</param>
    /// <param name="root">The root element of its content, whose actual theme decides the colors.</param>
    public static void Extend(Window window, FrameworkElement root)
    {
        var icon = new Image
        {
            Width = 16,
            Height = 16,
            Margin = new Thickness(12, 0, 8, 0),
            Source = new BitmapImage(new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", "squill-32.png"))),
        };
        var title = new TextBlock { FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        var bar = new Border
        {
            Height = 32,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { icon, title } },
        };
        var content = (UIElement)window.Content;
        var frame = new Grid { RowDefinitions = { new RowDefinition { Height = GridLength.Auto }, new RowDefinition() } };
        window.Content = frame;
        frame.Children.Add(bar);
        Grid.SetRow((FrameworkElement)content, 1);
        frame.Children.Add(content);
        window.ExtendsContentIntoTitleBar = true;
        window.SetTitleBar(bar);
        void Repaint()
        {
            title.Text = window.Title;
            title.Foreground = Tokens.Brush(root, "SquillFg2Brush");
            bar.Background = Tokens.Brush(root, "SquillBgBaseBrush");
            bar.BorderBrush = Tokens.Brush(root, "SquillBorderSubtleBrush");
            Paint(window.AppWindow.TitleBar, root, true);
        }

        root.Loaded += (_, _) => Repaint();
        root.ActualThemeChanged += (_, _) => Repaint();
    }

    /// <summary>Makes a secondary window follow the theme of the main window, caption bar included, for as long as it is open.</summary>
    /// <param name="window">The secondary window.</param>
    /// <param name="root">The root element of its content.</param>
    /// <param name="ownerRoot">The root element of the main window, whose requested theme is the app theme.</param>
    public static void Follow(Window window, FrameworkElement root, FrameworkElement ownerRoot)
    {
        void Sync() => root.RequestedTheme = ownerRoot.RequestedTheme;
        void Resync(FrameworkElement sender, object args) => Sync();
        Sync();
        Extend(window, root);
        ownerRoot.ActualThemeChanged += Resync;
        window.Closed += (_, _) => ownerRoot.ActualThemeChanged -= Resync;
    }
}
