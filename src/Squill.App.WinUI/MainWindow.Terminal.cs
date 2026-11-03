using Squill.Presentation.Terminal;
using Squill.Presentation.Workspace;
#if SQUILL_LOOMTERMINAL
using System.Collections.Specialized;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Squill.App.WinUI.Views;
using Squill.Core;
using Squill.Presentation.Localization;
#endif

namespace Squill.App.WinUI;

/// <summary>The terminal tabs of the shell; they exist only when the build references LoomTerminal.</summary>
public sealed partial class MainWindow
{
    // Without LoomTerminal the implementations are absent and the calls compile away.
    partial void AttachTerminals(WorkspaceViewModel workspace);

    partial void ShowTerminal(TerminalTabViewModel terminal);

#if SQUILL_LOOMTERMINAL
    private readonly Dictionary<TerminalTabViewModel, (TabViewItem Item, TerminalTabView View)> _terminals = [];

    partial void AttachTerminals(WorkspaceViewModel workspace)
    {
        var menu = new MenuFlyoutSubItem { Text = UiText.Get("menu.newTerminal") };
        FillTerminalMenu(menu, workspace);
        workspace.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WorkspaceViewModel.ActiveSession))
                FillTerminalMenu(menu, workspace);
        };
        ToolsMenu.Items.Add(menu);
        workspace.Terminals.CollectionChanged += OnTerminalsChanged;
    }

    private static void FillTerminalMenu(MenuFlyoutSubItem menu, WorkspaceViewModel workspace)
    {
        menu.Items.Clear();
        AddTerminalEntry(menu, workspace, UiText.Get("terminal.localShell"), null);
        if (workspace.ActiveSession is { } session)
            AddTerminalEntry(menu, workspace, $"{session.Profile.Name} CLI", session.Profile);
    }

    private static void AddTerminalEntry(MenuFlyoutSubItem menu, WorkspaceViewModel workspace, string text, ConnectionProfile? profile)
    {
        var entry = new MenuFlyoutItem { Text = text };
        entry.Click += (_, _) => workspace.NewTerminalCommand.Execute(profile);
        menu.Items.Add(entry);
    }

    private void OnTerminalsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var old in e.OldItems?.OfType<TerminalTabViewModel>() ?? [])
        {
            if (!_terminals.Remove(old, out var entry))
                continue;
            entry.View.Detach();
            TerminalArea.Children.Remove(entry.View);
            Tabs.TabItems.Remove(entry.Item);
        }

        foreach (var added in e.NewItems?.OfType<TerminalTabViewModel>() ?? [])
        {
            var view = new TerminalTabView(added) { Visibility = Visibility.Collapsed };
            var item = TabHeader.Create(added);
            _terminals[added] = (item, view);
            TerminalArea.Children.Add(view);
            Tabs.TabItems.Add(item);
            Tabs.SelectedItem = item;
        }
    }

    partial void ShowTerminal(TerminalTabViewModel terminal)
    {
        HideExtraAreas();
        ActiveTabView.Visibility = Visibility.Collapsed;
        TerminalArea.Visibility = Visibility.Visible;
        foreach (var (tab, entry) in _terminals)
            entry.View.Visibility = tab == terminal ? Visibility.Visible : Visibility.Collapsed;
        if (_terminals.TryGetValue(terminal, out var shown))
            shown.View.FocusTerminal();
    }
#endif
}
