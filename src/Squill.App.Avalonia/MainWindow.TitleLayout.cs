using Avalonia;
using Avalonia.Controls;

namespace Squill.App.Avalonia;

// Narrow window: the search box shrinks first; then the menu folds into a hamburger button. macOS keeps its menu as is (it has a menu bar of its own).
public sealed partial class MainWindow
{
    private double _menuWidth;
    private bool _menuCollapsed;
    private MenuFlyout? _hamburgerFlyout;

    private void InitializeTitleLayout()
    {
        if (OperatingSystem.IsMacOS())
            return;
        TitleBar.SizeChanged += (_, _) => UpdateTitleLayout();
        Opened += (_, _) => UpdateTitleLayout();
    }

    // The search box keeps at least 200 px; when the menu bar would leave it less, the menu items move into the hamburger flyout (moved, not copied, so bindings stay).
    private void UpdateTitleLayout()
    {
        if (_menuWidth <= 0 && !_menuCollapsed)
            _menuWidth = MainMenu.Bounds.Width;
        if (_menuWidth <= 0)
            return;
        var inset = WindowDecorationMargin.Right;
        TitleGrid.ColumnDefinitions[2].Width = new GridLength(inset);
        var room = TitleBar.Bounds.Width - inset - (14 + 16 + 8) - _menuWidth - 24;
        var collapse = room < 200;
        SetMenuCollapsed(collapse);
    }

    internal bool IsMenuCollapsed => _menuCollapsed;

    internal void SetMenuCollapsed(bool collapse)
    {
        if (collapse == _menuCollapsed)
            return;
        _menuCollapsed = collapse;
        if (_hamburgerFlyout is null)
        {
            _hamburgerFlyout = new MenuFlyout();
            HamburgerButton.Flyout = _hamburgerFlyout;
        }

        var (from, to) = collapse ? (MainMenu.Items, _hamburgerFlyout.Items) : (_hamburgerFlyout.Items, MainMenu.Items);
        foreach (var item in from.Cast<object>().ToList())
        {
            from.Remove(item);
            to.Add(item);
        }

        HamburgerButton.IsVisible = collapse;
        MainMenu.IsVisible = !collapse;
    }
}
