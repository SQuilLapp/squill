using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Squill.Presentation.Localization;

namespace Squill.App.Avalonia;

// macOS chrome: the window menu moves to the system menu bar (mirrored from MainMenu, which stays the one definition), About,
// Settings and updates go to the application menu, Ctrl shortcuts become Cmd, and a double click on the title bar zooms the window.
public sealed partial class MainWindow
{
    private void InitializeMacChrome()
    {
        if (!OperatingSystem.IsMacOS())
            return;
        MainMenu.IsVisible = false;
        TitleIcon.IsVisible = false;
        // Room for the traffic lights, which macOS draws over the extended client area.
        TitleStart.Margin = new Thickness(78, 0, 0, 0);
        TitleBar.DoubleTapped += (_, e) =>
        {
            // Only the empty title bar: the controls inside it keep their own double click.
            if (ReferenceEquals(e.Source, TitleBar))
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        };
    }

    // Called once the menu is complete (Attach adds the terminal entries to the Tools menu).
    private void InitializeMacMenu()
    {
        if (!OperatingSystem.IsMacOS() || Application.Current is not { } app)
            return;
        var bar = new NativeMenu();
        foreach (var item in MainMenu.Items.OfType<MenuItem>())
        {
            if (Mirror(item) is { } native)
                bar.Items.Add(native);
        }

        NativeMenu.SetMenu(this, bar);
        // Avalonia exports its default application menu at startup and does not follow a replaced menu: edit that one in place,
        // swapping its "About Avalonia" for ours and keeping Services, Hide and Quit.
        var appMenu = NativeMenu.GetMenu(app);
        if (appMenu is null)
        {
            appMenu = new NativeMenu();
            NativeMenu.SetMenu(app, appMenu);
        }

        if (appMenu.Items.Count > 0 && appMenu.Items[0] is NativeMenuItem { Header: "About Avalonia" })
            appMenu.Items.RemoveAt(0);
        appMenu.Items.Insert(0, Mirror(AboutMenuItem, appMenu: true)!);
        appMenu.Items.Insert(1, new NativeMenuItemSeparator());
        appMenu.Items.Insert(2, Mirror(SettingsMenuItem, appMenu: true)!);
        appMenu.Items.Insert(3, Mirror(UpdatesMenuItem, appMenu: true)!);
        foreach (var item in appMenu.Items.OfType<NativeMenuItem>())
            item.Header = LocalizedDefault(item.Header, app.Name ?? string.Empty) ?? item.Header;
    }

    /// <summary>Translates the English items Avalonia adds to the application menu.</summary>
    /// <param name="header">The item text set by Avalonia.</param>
    /// <param name="appName">The application name shown in Hide and Quit.</param>
    /// <returns>The catalog text, or null for an item that is not one of Avalonia's defaults.</returns>
    internal static string? LocalizedDefault(string? header, string appName) => header switch
    {
        "Services" => UiText.Get("menu.macServices"),
        "Hide Others" => UiText.Get("menu.macHideOthers"),
        "Show All" => UiText.Get("menu.macShowAll"),
        "Quit" => UiText.Format("menu.macQuit", appName),
        _ when header == "Hide " + appName => UiText.Format("menu.macHide", appName),
        _ when header == "Quit " + appName => UiText.Format("menu.macQuit", appName),
        _ => null,
    };

    /// <summary>Builds the native menu item that mirrors a window menu item: same text, command and click, Ctrl shortcuts turned into Cmd.</summary>
    /// <param name="item">The window menu item.</param>
    /// <param name="appMenu">True to mirror an item of the <c>appmenu</c> class, which the menu bar otherwise leaves to the application menu.</param>
    /// <returns>The native item, or null for an application-menu item or a submenu left empty.</returns>
    internal static NativeMenuItem? Mirror(MenuItem item, bool appMenu = false)
    {
        if (!appMenu && item.Classes.Contains("appmenu"))
            return null;
        var native = new NativeMenuItem(item.Header as string ?? string.Empty) { Gesture = CommandGesture(item.InputGesture) };
        if (!item.IsEnabled)
            native.IsEnabled = false;
        if (item.Items.Count > 0)
        {
            var menu = new NativeMenu();
            FillMirror(menu, item);
            if (menu.Items.Count == 0)
                return null;
            // Submenus filled on opening (the terminal list) refresh their content the same way.
            menu.NeedsUpdate += (_, _) =>
            {
                item.RaiseEvent(new RoutedEventArgs(MenuItem.SubmenuOpenedEvent));
                FillMirror(menu, item);
            };
            native.Menu = menu;
            return native;
        }

        // Bound, not copied: commands such as ActiveTab.RunCommand change with the active tab.
        native.Bind(NativeMenuItem.CommandProperty, item.GetObservable(MenuItem.CommandProperty));
        native.Bind(NativeMenuItem.CommandParameterProperty, item.GetObservable(MenuItem.CommandParameterProperty));
        native.Click += (_, _) =>
        {
            if (native.Command is null)
                item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        };
        return native;
    }

    private static void FillMirror(NativeMenu menu, MenuItem source)
    {
        menu.Items.Clear();
        foreach (var child in source.Items)
        {
            NativeMenuItemBase? native = child switch
            {
                Separator => new NativeMenuItemSeparator(),
                MenuItem item => Mirror(item),
                _ => null,
            };
            // No separator first, last or twice in a row once the application-menu items are gone.
            if (native is NativeMenuItemSeparator && (menu.Items.Count == 0 || menu.Items[^1] is NativeMenuItemSeparator))
                continue;
            if (native is not null)
                menu.Items.Add(native);
        }

        if (menu.Items.Count > 0 && menu.Items[^1] is NativeMenuItemSeparator)
            menu.Items.RemoveAt(menu.Items.Count - 1);
    }

    // Ctrl+X becomes Cmd+X. Gestures without Ctrl (Escape) are left out: a menu key equivalent would take the key from the editor and dialogs.
    private static KeyGesture? CommandGesture(KeyGesture? gesture) =>
        gesture is { } g && g.KeyModifiers.HasFlag(KeyModifiers.Control)
            ? new KeyGesture(g.Key, (g.KeyModifiers & ~KeyModifiers.Control) | KeyModifiers.Meta)
            : null;
}
