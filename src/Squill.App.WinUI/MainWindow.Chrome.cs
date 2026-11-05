using System.ComponentModel;
using System.Windows.Input;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Squill.App.WinUI.Views;
using Squill.Presentation.Localization;
using Squill.Presentation.Query;
using Squill.Presentation.Workspace;
using Windows.Graphics;
using Windows.System;

namespace Squill.App.WinUI;

/// <summary>The title bar with its menu and command search, the activity rail and the window size.</summary>
public sealed partial class MainWindow
{
    private const int DefaultWidth = 1440;
    private const int DefaultHeight = 900;
    private const int MinWidth = 720;
    private const int MinHeight = 600;

    private readonly List<(MenuFlyoutItem Item, Func<ICommand?> Command, Func<object?> Parameter)> _commandItems = [];
    private readonly List<(Button Button, FrameworkElement Icon, Rectangle Bar, SidebarPanel Panel)> _rail = [];
    private SidebarPanel _activePanel;
    private FrameworkElement _railDiagram = null!;
    private FrameworkElement _railSettings = null!;
    private FrameworkElement _commandSearchIcon = null!;
    private string? _dataDirectory;
    private double _menuWidth;
    private bool _menuCollapsed;
    private readonly List<(MenuBarItem Menu, MenuFlyoutSubItem Sub)> _hamburger = [];
    private QueryTabViewModel? _menuTab;

    private void InitializeChrome()
    {
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBar);
        TitleIcon.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "squill-32.png")));
        BuildMenu();
        BuildHamburger();
        BuildRail();
        BuildCommandSearch();
        Root.ActualThemeChanged += (_, _) => PaintChrome();
        TitleBar.SizeChanged += (_, _) => UpdateTitleLayout();
        Root.Loaded += (_, _) =>
        {
            SizeWindow();
            _menuWidth = MainMenu.ActualWidth;
            UpdateTitleLayout();
            Sidebar.Focus(FocusState.Programmatic);
            PaintChrome();
        };
    }

    // Content area 1440 x 900 logical px (spec section 2): the client area equals the window because the title bar is extended.
    private void SizeWindow()
    {
        var scale = Root.XamlRoot.RasterizationScale;
        AppWindow.ResizeClient(new SizeInt32((int)(DefaultWidth * scale), (int)(DefaultHeight * scale)));
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = (int)(MinWidth * scale);
            presenter.PreferredMinimumHeight = (int)(MinHeight * scale);
        }
    }

    private void PaintChrome()
    {
        TitleBar.Background = Tokens.Brush(Root, "SquillBgBaseBrush");
        TitleBar.BorderBrush = Tokens.Brush(Root, "SquillBorderSubtleBrush");
        CommandSearch.Background = Tokens.Brush(Root, "SquillBgRaisedBrush");
        CommandSearch.BorderBrush = Tokens.Brush(Root, "SquillBorderBrush");
        CommandSearchText.Foreground = Tokens.Brush(Root, "SquillFg3Brush");
        Icons.SetStroke(_commandSearchIcon, Tokens.Brush(Root, "SquillFg3Brush"));
        ActivityRail.Background = Tokens.Brush(Root, "SquillBgSurfaceBrush");
        ActivityRail.BorderBrush = Tokens.Brush(Root, "SquillBorderSubtleBrush");
        PaintRail();
        TabStripBar.Background = Tokens.Brush(Root, "SquillBgSurfaceBrush");
        TabStripBar.BorderBrush = Tokens.Brush(Root, "SquillBorderSubtleBrush");
        CaptionBar.Paint(AppWindow.TitleBar, Root, true);
    }

    private void BuildCommandSearch()
    {
        // Placeholder for the command palette, which does not exist yet.
        _commandSearchIcon = Icons.Create("SquillIconSearch", 14, Tokens.Brush(Root, "SquillFg3Brush"));
        _commandSearchIcon.Margin = new Thickness(0, 0, 8, 0);
        CommandSearchRow.Children.Insert(0, _commandSearchIcon);
        CommandSearchText.Text = UiText.Get("search.placeholder");
        CommandSearch.IsHitTestVisible = false;
    }

    private void BuildMenu()
    {
        MainMenu.Items.Add(Menu("menu.file",
            Item("menu.newTab", () => Workspace.NewTabCommand, null, OnNewTabClicked, VirtualKey.T, VirtualKeyModifiers.Control),
            Item("menu.closeTab", () => Workspace.CloseTabCommand, () => Workspace.ActiveTab, OnCloseTabClicked, VirtualKey.W, VirtualKeyModifiers.Control),
            new MenuFlyoutSeparator(),
            Plain("menu.settings", OnSettingsClicked, (VirtualKey)188, VirtualKeyModifiers.Control),
            Plain("menu.openDataFolder", OnOpenDataFolder)));
        MainMenu.Items.Add(Menu("menu.edit", Disabled("menu.undo"), Disabled("menu.redo")));
        MainMenu.Items.Add(Menu("menu.view", Item("menu.erDiagram", () => Workspace.OpenDiagramCommand, null, OnDiagramClicked)));
        MainMenu.Items.Add(Menu("menu.query",
            Item("editor.run", () => Workspace.ActiveTab?.RunCommand, null, OnRunClicked, VirtualKey.Enter, VirtualKeyModifiers.Control),
            Item("menu.runScript", () => Workspace.ActiveTab?.RunAllCommand, null, OnRunAllClicked),
            Item("editor.stop", () => Workspace.ActiveTab?.CancelCommand, null, OnCancelClicked, VirtualKey.Escape, VirtualKeyModifiers.None),
            new MenuFlyoutSeparator(),
            Item("menu.format", () => Workspace.ActiveTab?.FormatDocumentCommand, null, OnFormatClicked, VirtualKey.F, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift)));
        MainMenu.Items.Add(Menu("menu.database", Item("menu.newTable", () => Workspace.NewTableCommand, null, OnNewTableClicked)));
        ToolsMenu = Menu("menu.tools", Plain("menu.settings", OnSettingsClicked));
        MainMenu.Items.Add(ToolsMenu);
        MainMenu.Items.Add(Menu("menu.help", Disabled("menu.about")));
    }

    private MenuBarItem ToolsMenu { get; set; } = null!;

    // Narrow window: the menu bar folds into a hamburger button whose flyout holds the same items (moved, never copied, so commands and shortcuts stay in sync).
    private void BuildHamburger()
    {
        ToolTipService.SetToolTip(HamburgerButton, UiText.Get("menu.main"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(HamburgerButton, UiText.Get("menu.main"));
        var flyout = new MenuFlyout();
        foreach (var menu in MainMenu.Items.OfType<MenuBarItem>())
        {
            var sub = new MenuFlyoutSubItem { Text = menu.Title };
            _hamburger.Add((menu, sub));
            flyout.Items.Add(sub);
        }

        HamburgerButton.Flyout = flyout;
    }

    /// <summary>Puts every menu item in the menu bar or in the hamburger flyout, according to the current mode.</summary>
    private void SyncMenuItems()
    {
        foreach (var (menu, sub) in _hamburger)
        {
            var (from, to) = _menuCollapsed ? (menu.Items, sub.Items) : (sub.Items, menu.Items);
            foreach (var item in from.ToList())
            {
                from.Remove(item);
                to.Add(item);
            }
        }
    }

    // The search box shrinks first (never below 80 px); when the menu bar would leave it less than 200 px, the menu becomes a hamburger.
    private void UpdateTitleLayout()
    {
        if (_menuWidth <= 0 && !_menuCollapsed)
            _menuWidth = MainMenu.ActualWidth;
        if (_menuWidth <= 0 || Root.XamlRoot is not { } xamlRoot)
            return;
        var inset = AppWindow.TitleBar.RightInset / xamlRoot.RasterizationScale;
        TitleGrid.ColumnDefinitions[2].Width = new GridLength(inset);
        var left = 14 + 16 + 8;
        var room = TitleBar.ActualWidth - inset - left - _menuWidth - 24;
        var collapse = room < 200;
        if (collapse == _menuCollapsed && HamburgerButton.Visibility == (collapse ? Visibility.Visible : Visibility.Collapsed))
            return;
        _menuCollapsed = collapse;
        HamburgerButton.Visibility = collapse ? Visibility.Visible : Visibility.Collapsed;
        MainMenu.Visibility = collapse ? Visibility.Collapsed : Visibility.Visible;
        SyncMenuItems();
    }

    private static MenuBarItem Menu(string key, params MenuFlyoutItemBase[] items)
    {
        // The template ignores Padding, so negative margins tighten the items to the mockup spacing.
        var menu = new MenuBarItem { Title = UiText.Get(key), FontSize = 13, Margin = new Thickness(-4, 0, -4, 0) };
        foreach (var item in items)
            menu.Items.Add(item);
        return menu;
    }

    private static MenuFlyoutItem Disabled(string key) => new() { Text = UiText.Get(key), IsEnabled = false };

    // Shortcuts are shown through KeyboardAcceleratorTextOverride only: the accelerators themselves live on the root grid.
    private static MenuFlyoutItem Plain(string key, RoutedEventHandler click, VirtualKey? hint = null, VirtualKeyModifiers modifiers = VirtualKeyModifiers.None)
    {
        var item = new MenuFlyoutItem { Text = UiText.Get(key) };
        item.Click += click;
        if (hint is { } virtualKey)
            item.KeyboardAcceleratorTextOverride = Hint(virtualKey, modifiers);
        return item;
    }

    private MenuFlyoutItem Item(string key, Func<ICommand?> command, Func<object?>? parameter, RoutedEventHandler click, VirtualKey? hint = null, VirtualKeyModifiers modifiers = VirtualKeyModifiers.None)
    {
        var item = Plain(key, click, hint, modifiers);
        _commandItems.Add((item, command, parameter ?? (() => null)));
        return item;
    }

    private static string Hint(VirtualKey key, VirtualKeyModifiers modifiers)
    {
        var parts = new List<string>();
        if (modifiers.HasFlag(VirtualKeyModifiers.Control))
            parts.Add("Ctrl");
        if (modifiers.HasFlag(VirtualKeyModifiers.Shift))
            parts.Add("Maj");
        parts.Add(key switch
        {
            VirtualKey.Enter => "Entrée",
            VirtualKey.Escape => "Échap",
            (VirtualKey)188 => ",",
            _ => key.ToString(),
        });
        return string.Join('+', parts);
    }

    // Item enabled states follow the commands, so the menu is refreshed when the workspace or the active tab changes.
    private void SyncMenu()
    {
        foreach (var (item, command, parameter) in _commandItems)
            item.IsEnabled = ChromeFormat.IsEnabled(command(), parameter());
    }

    private void AttachMenu(WorkspaceViewModel workspace)
    {
        workspace.PropertyChanged += (_, _) => WatchMenuTab();
        workspace.OpenDiagramCommand.CanExecuteChanged += (_, _) => SyncMenu();
        workspace.NewTableCommand.CanExecuteChanged += (_, _) => SyncMenu();
        WatchMenuTab();
    }

    private void WatchMenuTab()
    {
        if (!ReferenceEquals(_menuTab, Workspace.ActiveTab))
        {
            if (_menuTab is not null)
                _menuTab.PropertyChanged -= OnMenuTabChanged;
            _menuTab = Workspace.ActiveTab;
            if (_menuTab is not null)
                _menuTab.PropertyChanged += OnMenuTabChanged;
        }

        SyncMenu();
    }

    private void OnMenuTabChanged(object? sender, PropertyChangedEventArgs e) => SyncMenu();

    private async void OnOpenDataFolder(object sender, RoutedEventArgs e)
    {
        if (_dataDirectory is null)
            return;
        Directory.CreateDirectory(_dataDirectory);
        await Launcher.LaunchFolderPathAsync(_dataDirectory);
    }

    private void BuildRail()
    {
        AddRail(RailExplorer, BarExplorer, "SquillIconExplorer", SidebarPanel.Explorer, "rail.explorer");
        AddRail(RailHistory, BarHistory, "SquillIconHistory", SidebarPanel.History, "editor.history");
        AddRail(RailSnippets, BarSnippets, "SquillIconSnippets", SidebarPanel.Snippets, "editor.snippets");
        _railDiagram = Icons.Create("SquillIconDiagram", 20, Tokens.Brush(Root, "SquillFg2Brush"));
        _railSettings = Icons.Create("SquillIconSettings", 20, Tokens.Brush(Root, "SquillFg2Brush"));
        RailDiagram.Content = _railDiagram;
        RailSettings.Content = _railSettings;
        ToolTipService.SetToolTip(RailDiagram, UiText.Get("menu.erDiagram"));
        ToolTipService.SetToolTip(RailSettings, UiText.Get("menu.settings"));
        ShowRailPanel(SidebarPanel.Explorer);
    }

    private void AddRail(Button button, Rectangle bar, string icon, SidebarPanel panel, string tooltipKey)
    {
        var glyph = Icons.Create(icon, 20, Tokens.Brush(Root, "SquillFg2Brush"));
        button.Content = glyph;
        ToolTipService.SetToolTip(button, UiText.Get(tooltipKey));
        _rail.Add((button, glyph, bar, panel));
        button.Click += (_, _) => ShowRailPanel(panel);
    }

    private void ShowRailPanel(SidebarPanel panel)
    {
        _activePanel = panel;
        Sidebar.ShowPanel(panel);
        PaintRail();
    }

    // The active item gets the accent-soft square and the 3 px accent bar on the rail's left edge.
    private void PaintRail()
    {
        Icons.SetStroke(_railDiagram, Tokens.Brush(Root, "SquillFg2Brush"));
        Icons.SetStroke(_railSettings, Tokens.Brush(Root, "SquillFg2Brush"));
        foreach (var (button, glyph, bar, panel) in _rail)
        {
            var active = panel == _activePanel;
            Icons.SetStroke(glyph, Tokens.Brush(Root, active ? "SquillAccentBrush" : "SquillFg2Brush"));
            button.Background = active ? Tokens.Brush(Root, "SquillAccentSoftBrush") : new SolidColorBrush(Colors.Transparent);
            bar.Fill = Tokens.Brush(Root, "SquillAccentBrush");
            bar.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
