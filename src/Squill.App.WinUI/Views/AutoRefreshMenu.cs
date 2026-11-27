using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Squill.Presentation.Localization;
using Squill.Presentation.Query;

namespace Squill.App.WinUI.Views;

/// <summary>The menu of the auto-refresh clock in the query tab toolbar; it only draws <see cref="QueryTabViewModel"/>, built from plain items in code (data-bound item lists break under Native AOT).</summary>
internal static class AutoRefreshMenu
{
    private const string Group = "AutoRefresh";

    /// <summary>Builds the menu; its items are rebuilt each time it opens so the checks follow the view-model.</summary>
    /// <param name="tab">The tab whose interval the menu edits.</param>
    /// <returns>The flyout to assign to the clock button.</returns>
    public static MenuFlyout Build(QueryTabViewModel tab)
    {
        var menu = new MenuFlyout { Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft };
        menu.Opening += (_, _) => Fill(menu, tab);
        return menu;
    }

    private static void Fill(MenuFlyout menu, QueryTabViewModel tab)
    {
        menu.Items.Clear();
        menu.Items.Add(new MenuFlyoutItem { Text = UiText.Get("query.autoRefresh.menu"), IsEnabled = false });
        var enabled = tab.CanRefresh || tab.AutoRefreshInterval is not null;
        menu.Items.Add(Option(UiText.Get("query.autoRefresh.disabled"), tab.AutoRefreshInterval is null, tab.SetAutoRefreshCommand, null, true));
        foreach (var preset in QueryTabViewModel.AutoRefreshPresets)
            menu.Items.Add(Option(QueryTabViewModel.FormatInterval(preset), tab.AutoRefreshInterval == preset, tab.SetAutoRefreshCommand, preset, enabled));
        menu.Items.Add(new MenuFlyoutSeparator());
        var custom = tab.IsAutoRefreshCustom;
        var text = custom ? UiText.Format("query.autoRefresh.customValue", tab.AutoRefreshLabel) : UiText.Get("query.autoRefresh.custom");
        menu.Items.Add(Option(text, custom, tab.SetCustomAutoRefreshCommand, null, enabled));
    }

    private static RadioMenuFlyoutItem Option(string text, bool isChecked, System.Windows.Input.ICommand command, object? parameter, bool enabled) => new()
    {
        Text = text,
        GroupName = Group,
        IsChecked = isChecked,
        Command = command,
        CommandParameter = parameter,
        IsEnabled = enabled,
    };
}
