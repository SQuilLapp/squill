using Squill.Core.Transactions;
using Squill.Presentation.Localization;
using Squill.Presentation.Query;
using Squill.Presentation.Results;
using Squill.Presentation.Workspace;

namespace Squill.Presentation.Palette;

/// <summary>The actions the palette can run: the workspace's own commands, plus those the heads add at startup.</summary>
public sealed class PaletteActionRegistry
{
    private readonly List<PaletteAction> _actions = [];

    /// <summary>Gets the registered actions in registration order.</summary>
    public IReadOnlyList<PaletteAction> All => _actions;

    /// <summary>Registers an action.</summary>
    /// <param name="action">The action to add.</param>
    /// <exception cref="ArgumentException">An action with the same id is already registered.</exception>
    public void Add(PaletteAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (_actions.Exists(a => a.Id == action.Id))
            throw new ArgumentException($"Palette action '{action.Id}' is already registered.", nameof(action));
        _actions.Add(action);
    }

    /// <summary>Finds the actions whose title or category matches, best first.</summary>
    /// <param name="term">The typed text without the mode prefix; empty returns every action in registration order.</param>
    /// <returns>Enabled actions first, then by score, then shorter titles first.</returns>
    public IReadOnlyList<PaletteItem> Search(string term)
    {
        ArgumentNullException.ThrowIfNull(term);
        List<PaletteItem> found = [];
        foreach (var action in _actions)
        {
            var title = UiText.Get(action.TitleKeySource?.Invoke() ?? action.TitleKey);
            var category = UiText.Get(action.CategoryKey);
            var byTitle = FuzzyMatcher.Match(title, term);
            var byCategory = byTitle is null ? FuzzyMatcher.Match(category, term) : null;
            if (byTitle is null && byCategory is null)
                continue;
            var enabled = action.CanRun();
            var run = action.Run;
            // A title match always outranks a category-only match.
            var score = byTitle is { } t ? 1000 + t.Score : byCategory!.Score;
            found.Add(new PaletteItem(
                PaletteItemKind.Action, title, category, null, null, action.Shortcut, action.IconKey,
                byTitle?.Ranges ?? [], enabled, score,
                (_, ct) => enabled && action.CanRun() ? run(ct) : ValueTask.CompletedTask));
        }

        if (term.Length == 0)
            return found;
        return [.. found.OrderByDescending(i => i.IsEnabled).ThenByDescending(i => i.Score).ThenBy(i => i.Title.Length)];
    }

    /// <summary>Creates a registry holding the actions the workspace can run itself.</summary>
    /// <param name="workspace">The workspace whose commands the actions call.</param>
    /// <returns>A registry the heads may add their own actions to.</returns>
    public static PaletteActionRegistry ForWorkspace(WorkspaceViewModel workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var registry = new PaletteActionRegistry();

        void Add(string id, string category, string? shortcut, string icon, Func<bool> can, Func<Task> run) =>
            registry.Add(new PaletteAction(id, "palette.action" + "." + id, category, shortcut, icon, can, _ => new ValueTask(run())));

        void OnTab(string id, string? shortcut, string icon, Func<QueryTabViewModel, bool> can, Func<QueryTabViewModel, Task> run) =>
            Add(id, "menu.query", shortcut, icon, () => workspace.ActiveTab is { } t && can(t), () => run(workspace.ActiveTab!));

        Add("workspace.newTab", "menu.file", "Ctrl+T", "action", () => workspace.NewTabCommand.CanExecute(null), () => Do(() => workspace.NewTabCommand.Execute(null)));
        Add("workspace.closeTab", "menu.file", "Ctrl+W", "action", () => workspace.ActiveTab is not null, () => workspace.CloseTabCommand.ExecuteAsync(workspace.ActiveTab));
        OnTab("query.run", "Ctrl+Enter", "action", _ => true, t => t.RunCommand.ExecuteAsync(null));
        OnTab("query.runAll", null, "action", _ => true, t => t.RunAllCommand.ExecuteAsync(null));
        OnTab("query.cancel", null, "action", t => t.IsRunning, t => Do(() => t.CancelCommand.Execute(null)));
        OnTab("query.format", "Ctrl+Shift+F", "action", _ => true, t => t.FormatDocumentCommand.ExecuteAsync(null));
        OnTab("query.explain", null, "action", t => t.CanExplain, t => t.ExplainCommand.ExecuteAsync(null));
        OnTab("query.analyze", null, "action", t => t.CanAnalyze, t => t.AnalyzeCommand.ExecuteAsync(null));
        OnTab("query.commit", null, "check", t => t.CanCommit, t => t.CommitCommand.ExecuteAsync(null));
        OnTab("query.rollback", null, "action", t => t.CanRollback, t => t.RollbackCommand.ExecuteAsync(null));
        registry.Add(new PaletteAction(
            "query.mode", "palette.action.query.mode.manual", "menu.query", null, "action",
            () => workspace.ActiveTab is { } t && t.Transaction.IsSupported && !t.IsRunning,
            _ => new ValueTask(workspace.ActiveTab!.SetModeCommand.ExecuteAsync(
                workspace.ActiveTab.Transaction.Mode == TransactionMode.Manual ? TransactionMode.AutoCommit : TransactionMode.Manual)),
            () => workspace.ActiveTab?.Transaction.Mode == TransactionMode.Manual ? "palette.action.query.mode.auto" : "palette.action.query.mode.manual"));
        Add("workspace.newTable", "menu.database", null, "action", () => workspace.NewTableCommand.CanExecute(null), () => Do(() => workspace.NewTableCommand.Execute(null)));
        Add("workspace.openDiagram", "menu.view", null, "action", () => workspace.OpenDiagramCommand.CanExecute(null), () => workspace.OpenDiagramCommand.ExecuteAsync(null));
        Add("workspace.openSchemaCompare", "menu.database", null, "action", () => workspace.OpenSchemaCompareCommand.CanExecute(null), () => Do(() => workspace.OpenSchemaCompareCommand.Execute(null)));
        Add("workspace.openSessionsMonitor", "menu.database", null, "action", () => workspace.OpenSessionsMonitorCommand.CanExecute(null), () => Do(() => workspace.OpenSessionsMonitorCommand.Execute(null)));
        Add("workspace.openSecurity", "menu.database", null, "action", () => workspace.OpenSecurityCommand.CanExecute(null), () => Do(() => workspace.OpenSecurityCommand.Execute(null)));
        Add("workspace.importData", "menu.database", null, "action", () => workspace.ImportDataCommand.CanExecute(null), () => workspace.ImportDataCommand.ExecuteAsync(null));
        Add("workspace.newTerminal", "menu.tools", null, "action", () => true, () => workspace.NewTerminalCommand.ExecuteAsync(null));
        Add("workspace.disconnect", "menu.database", null, "action", () => workspace.DisconnectCommand.CanExecute(null), () => workspace.DisconnectCommand.ExecuteAsync(null));
        Add("grid.clearFilter", "menu.view", null, "action", () => ActiveView(workspace)?.IsActive == true, () => Do(() => ActiveView(workspace)?.ClearFilters()));
        return registry;
    }

    private static ResultViewState? ActiveView(WorkspaceViewModel workspace) => workspace.ActiveTab?.SelectedResult?.View;

    private static Task Do(Action action)
    {
        action();
        return Task.CompletedTask;
    }
}
