using Avalonia;
using Avalonia.Controls;

namespace Squill.App.Avalonia;

// Narrow window: the Properties panel hides, the search box shrinks, then the menu folds into a hamburger button. macOS keeps its menu as is (it has a menu bar of its own).
public sealed partial class MainWindow
{
    private double _menuWidth;
    private bool _menuCollapsed;
    private MenuFlyout? _hamburgerFlyout;

    // Below this window width the Properties panel gives its 264 px back to the editor (the designer needs them most).
    internal const double PropertiesMinWindowWidth = 1280;

    private void InitializeTitleLayout()
    {
        RootGrid.SizeChanged += (_, e) =>
        {
            PropertiesHost.IsVisible = e.NewSize.Width >= PropertiesMinWindowWidth;
            SyncPropertiesColumn();
        };
        Properties.PropertyChanged += (_, e) =>
        {
            if (e.Property == IsVisibleProperty)
                SyncPropertiesColumn();
        };
        if (OperatingSystem.IsMacOS())
            return;
        TitleBar.SizeChanged += (_, _) => UpdateTitleLayout();
        Opened += (_, _) => UpdateTitleLayout();
    }

    // A hidden Properties panel gives its whole column back (the width the user dragged stays in Width, only the limits change).
    private void SyncPropertiesColumn()
    {
        var shown = PropertiesHost.IsVisible && Properties.IsVisible;
        PropertiesSplitter.IsVisible = shown;
        var column = RootGrid.ColumnDefinitions[5];
        column.MinWidth = shown ? 120 : 0;
        column.MaxWidth = shown ? double.PositiveInfinity : 0;
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
