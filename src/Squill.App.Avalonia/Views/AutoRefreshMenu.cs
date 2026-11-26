using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Squill.Presentation.Localization;
using Squill.Presentation.Query;

namespace Squill.App.Avalonia.Views;

/// <summary>The menu of the auto-refresh clock in the query tab toolbar; it only draws <see cref="QueryTabViewModel"/>, built from plain items in code.</summary>
internal static class AutoRefreshMenu
{
    private const string Group = "AutoRefresh";

    /// <summary>Builds the menu from the current state of the tab; build it again each time it opens so the checks follow the view-model.</summary>
    /// <param name="tab">The tab whose interval the menu edits.</param>
    /// <returns>The flyout to show under the clock button.</returns>
    public static MenuFlyout Build(QueryTabViewModel tab)
    {
        var menu = new MenuFlyout { Placement = PlacementMode.BottomEdgeAlignedLeft };
        var items = new List<Control>
        {
            new MenuItem { Header = UiText.Get("query.autoRefresh.menu"), IsEnabled = false },
        };
        var enabled = tab.CanRefresh || tab.AutoRefreshInterval is not null;
        items.Add(Option(UiText.Get("query.autoRefresh.disabled"), tab.AutoRefreshInterval is null, tab.SetAutoRefreshCommand, null, true));
        foreach (var preset in QueryTabViewModel.AutoRefreshPresets)
            items.Add(Option(QueryTabViewModel.FormatInterval(preset), tab.AutoRefreshInterval == preset, tab.SetAutoRefreshCommand, preset, enabled));
        items.Add(new Separator());
        var custom = tab.IsAutoRefreshCustom;
        var text = custom ? UiText.Format("query.autoRefresh.customValue", tab.AutoRefreshLabel) : UiText.Get("query.autoRefresh.custom");
        items.Add(Option(text, custom, tab.SetCustomAutoRefreshCommand, null, enabled));
        menu.ItemsSource = items;
        return menu;
    }

    private static MenuItem Option(string text, bool isChecked, System.Windows.Input.ICommand command, object? parameter, bool enabled) => new()
    {
        Header = text,
        ToggleType = MenuItemToggleType.Radio,
        GroupName = Group,
        IsChecked = isChecked,
        Command = command,
        CommandParameter = parameter,
        IsEnabled = enabled,
    };
}
