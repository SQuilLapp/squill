using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Squill.App.Avalonia.Controls;
using Squill.App.Avalonia.Views;
using Squill.Core;
using Squill.Data;
using Squill.Data.Sqlite;
using Squill.Platform;
using Squill.Platform.Storage;
using Squill.Presentation;
using Squill.Presentation.Designer;
using Xunit;

namespace Squill.App.Avalonia.Tests;

[Collection("Headless")]
public sealed class DesignerUiTests
{
    private static async Task WithDesignerAsync(Func<MainWindow, AppServices, TableDesignerViewModel, Task> body)
    {
        var dir = Path.Combine(Path.GetTempPath(), "squill-designer-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            await Headless.Session.Dispatch(async () =>
            {
                var providers = new ProviderRegistry();
                providers.Register(new SqliteDatabaseProvider());
                var paths = new AppPaths(dir);
                using var store = new JsonDiagramLayoutStore(paths);
                var services = AppComposition.Create(new TestPlatform(providers), paths, store, ErDiagramCanvas.MeasureText);
                var window = new MainWindow { DataContext = services.Workspace, Width = 1100, Height = 700 };
                window.Attach(services, dir);
                var profile = new ConnectionProfile(Guid.NewGuid(), "test", DatabaseDialect.Sqlite, null, null, null, null, Path.Combine(dir, "t.db"), null);
                await services.Workspace.ConnectCommand.ExecuteAsync(profile);
                services.Workspace.NewTableCommand.Execute(null);
                window.Show();
                try
                {
                    await body(window, services, services.Workspace.ActiveDesigner!);
                }
                finally
                {
                    window.Close();
                }
            }, TestContext.Current.CancellationToken);
        }
        finally
        {
            try
            {
                Directory.Delete(dir, true);
            }
            catch (IOException)
            {
            }
        }
    }

    [Fact]
    public async Task AddingAColumn_UpdatesTheScriptPreview()
    {
        await WithDesignerAsync((window, _, designer) =>
        {
            var view = window.GetLogicalDescendants().OfType<TableDesignerView>().Single();
            Assert.True(view.IsVisible);
            Assert.Same(designer, view.DataContext);
            var editor = view.GetVisualDescendants().OfType<AvaloniaEdit.TextEditor>().Single(t => t.Name == "ScriptEditor");
            Assert.DoesNotContain("new_column", editor.Text);
            designer.AddColumnCommand.Execute(null);
            Assert.Contains("new_column", editor.Text);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task ValidationError_ShowsInIssuesAndDisablesApply()
    {
        await WithDesignerAsync((window, _, designer) =>
        {
            var view = window.GetLogicalDescendants().OfType<TableDesignerView>().Single();
            var apply = view.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "ApplyButton");
            Assert.True(apply.Command!.CanExecute(null));
            designer.Name = "";
            Assert.True(designer.HasErrors);
            var issues = view.GetVisualDescendants().OfType<ListBox>().Single(l => l.Name == "IssueList");
            Assert.Equal(designer.Issues.Count, issues.ItemCount);
            Assert.True(issues.ItemCount > 0);
            Assert.False(apply.Command!.CanExecute(null));
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task ModifiedAndNewRows_ShowTheirTagAndDraftState()
    {
        await WithDesignerAsync(async (window, services, designer) =>
        {
            designer.Name = "tagged";
            await designer.ApplyCommand.ExecuteAsync(null);
            Assert.NotNull(designer.Original);
            var view = window.GetLogicalDescendants().OfType<TableDesignerView>().Single();
            IEnumerable<string?> VisibleTags() => view.GetVisualDescendants().OfType<TextBlock>()
                .Where(t => t.IsEffectivelyVisible && t.Text is "MODIFIÉE" or "NOUVELLE").Select(t => t.Text);
            Assert.Empty(VisibleTags());
            Assert.Equal("", designer.DraftText);

            designer.Columns[0].DataType = "text";
            designer.AddColumnCommand.Execute(null);
            window.UpdateLayout();
            Assert.Equal(["MODIFIÉE", "NOUVELLE"], VisibleTags().Order(StringComparer.Ordinal).Reverse());
            Assert.True(designer.Columns[0].IsModified);
            Assert.True(designer.Columns[1].IsNew);
            Assert.Contains("brouillon", designer.DraftText);
            Assert.Same(designer.Columns[1], designer.SelectedColumn);
        });
    }

    [Fact]
    public async Task CtrlS_AppliesTheActiveDesigner()
    {
        await WithDesignerAsync(async (window, services, designer) =>
        {
            designer.Name = "made_in_ui";
            window.KeyPressQwerty(PhysicalKey.S, RawInputModifiers.Control);
            for (var i = 0; i < 100 && services.Workspace.ActiveSession!.Schema.FindTable(null, "made_in_ui") is null; i++)
                await Task.Delay(20, TestContext.Current.CancellationToken);
            Assert.NotNull(services.Workspace.ActiveSession!.Schema.FindTable(null, "made_in_ui"));
        });
    }

    [Fact]
    public async Task ForeignKeyTab_BindsTheRows()
    {
        await WithDesignerAsync((window, _, designer) =>
        {
            designer.AddForeignKeyCommand.Execute(null);
            var view = window.GetLogicalDescendants().OfType<TableDesignerView>().Single();
            view.GetVisualDescendants().OfType<TabControl>().First().SelectedIndex = 2;
            window.UpdateLayout();
            var rows = view.GetVisualDescendants().OfType<ItemsControl>().Single(c => c.Name == "ForeignKeyRows");
            Assert.Equal(1, rows.ItemCount);
            Assert.Equal(2, rows.GetVisualDescendants().OfType<ComboBox>().Count());
            return Task.CompletedTask;
        });
    }
}
