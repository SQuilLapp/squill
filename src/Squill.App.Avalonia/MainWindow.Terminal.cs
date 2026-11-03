using Squill.Presentation.Terminal;
using Squill.Presentation.Workspace;
#if SQUILL_LOOMTERMINAL
using System.Collections.Specialized;
using Avalonia.Controls;
using Squill.App.Avalonia.Views;
using Squill.Core;
using Squill.Presentation.Localization;
#endif

namespace Squill.App.Avalonia;

/// <summary>The terminal tabs of the shell; they exist only when the build references LoomTerminal.</summary>
public sealed partial class MainWindow
{
    // Without LoomTerminal the implementations are absent and the calls compile away.
    partial void AttachTerminals(WorkspaceViewModel workspace);

    partial void ShowTerminal(TerminalTabViewModel terminal);

#if SQUILL_LOOMTERMINAL
    private readonly Dictionary<TerminalTabViewModel, TerminalTabView> _terminals = [];

    partial void AttachTerminals(WorkspaceViewModel workspace)
    {
        var menu = new MenuItem { Header = UiText.Get("menu.newTerminal") };
        FillTerminalMenu(menu, workspace);
        menu.SubmenuOpened += (_, _) => FillTerminalMenu(menu, workspace);
        ToolsMenu.Items.Add(menu);
        workspace.Terminals.CollectionChanged += OnTerminalsChanged;
    }

    private static void FillTerminalMenu(MenuItem menu, WorkspaceViewModel workspace)
    {
        menu.Items.Clear();
        AddTerminalEntry(menu, workspace, UiText.Get("terminal.localShell"), null);
        if (workspace.ActiveSession is { } session)
            AddTerminalEntry(menu, workspace, $"{session.Profile.Name} CLI", session.Profile);
    }

    private static void AddTerminalEntry(MenuItem menu, WorkspaceViewModel workspace, string text, ConnectionProfile? profile)
    {
        var entry = new MenuItem { Header = text };
        entry.Click += (_, _) => workspace.NewTerminalCommand.Execute(profile);
        menu.Items.Add(entry);
    }

    private void OnTerminalsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var old in e.OldItems?.OfType<TerminalTabViewModel>() ?? [])
        {
            if (!_terminals.Remove(old, out var view))
                continue;
            view.Detach();
            TerminalArea.Children.Remove(view);
        }

        foreach (var added in e.NewItems?.OfType<TerminalTabViewModel>() ?? [])
        {
            var view = new TerminalTabView(added) { IsVisible = false };
            _terminals[added] = view;
            TerminalArea.Children.Add(view);
            if (_tabs?.Selected?.Source == added)
                ShowTerminal(added);
        }
    }

    partial void ShowTerminal(TerminalTabViewModel terminal)
    {
        TerminalArea.IsVisible = true;
        foreach (var (tab, view) in _terminals)
            view.IsVisible = tab == terminal;
        if (_terminals.TryGetValue(terminal, out var shown))
            shown.FocusTerminal();
    }
#endif
}
