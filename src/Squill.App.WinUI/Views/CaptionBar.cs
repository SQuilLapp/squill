using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

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

    /// <summary>Makes a secondary window follow the theme of the main window, caption bar included, for as long as it is open.</summary>
    /// <param name="window">The secondary window.</param>
    /// <param name="root">The root element of its content.</param>
    /// <param name="ownerRoot">The root element of the main window, whose requested theme is the app theme.</param>
    public static void Follow(Window window, FrameworkElement root, FrameworkElement ownerRoot)
    {
        void Sync() => root.RequestedTheme = ownerRoot.RequestedTheme;
        void Repaint(FrameworkElement sender, object args) => Paint(window.AppWindow.TitleBar, root, false);
        Sync();
        ownerRoot.ActualThemeChanged += Resync;
        root.ActualThemeChanged += Repaint;
        root.Loaded += (_, _) => Repaint(root, null!);
        window.Closed += (_, _) => ownerRoot.ActualThemeChanged -= Resync;

        void Resync(FrameworkElement sender, object args) => Sync();
    }
}
