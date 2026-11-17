using Squill.Core;
using Squill.Core.Transactions;
using Squill.Data;
using Squill.Presentation.Localization;
using Squill.Presentation.Palette;
using Squill.Presentation.Panels;
using Squill.Presentation.Query;
using Squill.Presentation.Results;
using Squill.Presentation.Tests.Panels;
using Xunit;

namespace Squill.Presentation.Tests.Palette;

public sealed class PaletteActionRegistryTests
{
    private sealed class EmptyReader : IResultReader
    {
        public IReadOnlyList<ColumnInfo> Columns { get; } = [new ColumnInfo("id", "int", typeof(int), false, 0)];
        public long RecordsAffected => -1;
        public ValueTask<object?[][]?> ReadBatchAsync(int maxRows, CancellationToken cancellationToken) => ValueTask.FromResult<object?[][]?>(null);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static PaletteItem Find(PaletteActionRegistry registry, string term, string title) =>
        Assert.Single(registry.Search(term), i => i.Title == title);

    [Fact]
    public void Add_DuplicateId_Throws()
    {
        var registry = new PaletteActionRegistry();
        PaletteAction Make() => new("a", "palette.action.query.run", "menu.query", null, "action", () => true, _ => ValueTask.CompletedTask);
        registry.Add(Make());

        Assert.Throws<ArgumentException>(() => registry.Add(Make()));
    }

    [Fact]
    public void Search_Commit_RanksCommitFirst()
    {
        using var _ = UiText.Override(UiLanguage.English);
        var registry = PaletteActionRegistry.ForWorkspace(new PanelsHost().Workspace);

        var items = registry.Search("comm");

        Assert.Equal("Commit — commit the transaction", items[0].Title);
        Assert.Equal("Query", items[0].Detail);
    }

    [Fact]
    public void Search_WithoutActiveTab_DisablesTabActionsAndRanksThemAfterEnabledOnes()
    {
        using var _ = UiText.Override(UiLanguage.English);
        var registry = PaletteActionRegistry.ForWorkspace(new PanelsHost().Workspace);

        var items = registry.Search("");

        Assert.False(Find(registry, "", "Run").IsEnabled);
        Assert.False(Find(registry, "", "New query tab").IsEnabled);
        Assert.True(Find(registry, "", "New terminal").IsEnabled);
        Assert.Equal(registry.All.Count, items.Count);
        var ranked = registry.Search("e");
        Assert.True(ranked.SkipWhile(i => i.IsEnabled).All(i => !i.IsEnabled));
    }

    [Fact]
    public async Task Run_NewTabAndCloseTab_CallTheWorkspaceCommands()
    {
        using var _ = UiText.Override(UiLanguage.English);
        var host = new PanelsHost();
        await host.ConnectAsync();
        var registry = PaletteActionRegistry.ForWorkspace(host.Workspace);
        var before = host.Workspace.Tabs.Count;

        var newTab = Find(registry, "", "New query tab");
        Assert.True(newTab.IsEnabled);
        Assert.Equal("Ctrl+T", newTab.Shortcut);
        await newTab.Run(PaletteRunMode.Default, CancellationToken.None);
        Assert.Equal(before + 1, host.Workspace.Tabs.Count);

        await Find(registry, "", "Close tab").Run(PaletteRunMode.Default, CancellationToken.None);
        Assert.Equal(before, host.Workspace.Tabs.Count);
    }

    [Fact]
    public async Task Run_DisabledItem_DoesNothing()
    {
        using var _ = UiText.Override(UiLanguage.English);
        var host = new PanelsHost();
        var registry = PaletteActionRegistry.ForWorkspace(host.Workspace);

        await Find(registry, "", "New query tab").Run(PaletteRunMode.Default, CancellationToken.None);

        Assert.Empty(host.Workspace.Tabs);
    }

    [Theory]
    [InlineData(UiLanguage.French, "Nouvel onglet de requête", "Fichier")]
    [InlineData(UiLanguage.English, "New query tab", "File")]
    public void Search_ResolvesTitleAndCategoryInTheCurrentLanguage(UiLanguage language, string title, string category)
    {
        using var _ = UiText.Override(language);
        var registry = PaletteActionRegistry.ForWorkspace(new PanelsHost().Workspace);

        var item = Find(registry, "", title);

        Assert.Equal(category, item.Detail);
    }

    [Fact]
    public async Task ClearFilter_IsEnabledOnlyWhileTheActiveResultHasAView()
    {
        using var _ = UiText.Override(UiLanguage.English);
        var host = new PanelsHost();
        await host.ConnectAsync();
        var tab = host.Workspace.Tabs[0];
        var result = ResultViewModel.ForGrid("r", new StreamingGridDataSource(new EmptyReader(), 10, new ImmediateDispatcher()), host.Dialogs, new ImmediateDispatcher());
        tab.Results.Add(result);
        tab.SelectedResult = result;
        var registry = PaletteActionRegistry.ForWorkspace(host.Workspace);
        Assert.False(Find(registry, "", "Clear the grid filter").IsEnabled);

        result.View!.SearchText = "x";
        var item = Find(registry, "", "Clear the grid filter");
        Assert.True(item.IsEnabled);
        await item.Run(PaletteRunMode.Default, CancellationToken.None);

        Assert.False(result.View.IsActive);
        Assert.False(Find(registry, "", "Clear the grid filter").IsEnabled);
    }

    [Fact]
    public async Task ModeAction_TitleFollowsTheTransactionState()
    {
        using var _ = UiText.Override(UiLanguage.English);
        var host = new PanelsHost(probe: true);
        await host.ConnectAsync();
        var registry = PaletteActionRegistry.ForWorkspace(host.Workspace);
        var manual = Find(registry, "", "Switch to manual transaction");
        Assert.True(manual.IsEnabled);

        await manual.Run(PaletteRunMode.Default, CancellationToken.None);

        Assert.Equal(TransactionMode.Manual, host.Workspace.ActiveTab!.Transaction.Mode);
        Assert.True(Find(registry, "", "Switch to auto-commit").IsEnabled);
    }
}
