using Avalonia.Controls;
using Avalonia.LogicalTree;
using Squill.App.Avalonia.Views;
using Squill.Core;
using Squill.Data;
using Squill.Platform;
using Squill.Platform.Profiles;
using Squill.Presentation;
using Squill.Presentation.Connections;
using Squill.Presentation.Query;
using Xunit;

namespace Squill.App.Avalonia.Tests;

[Collection("Headless")]
public sealed class ConnectionUiTests
{
    [Fact]
    public async Task Editor_SshSection_VisibleOnlyWhenUseSsh()
    {
        await Headless.Session.Dispatch(() =>
        {
            var editor = new ConnectionEditorViewModel(null, new NullStore(), new ProviderRegistry(), new Dialogs());
            var window = new ConnectionEditorWindow { DataContext = editor };
            window.FindControl<TabControl>("Tabs")!.SelectedIndex = 1;
            var ssh = window.GetLogicalDescendants().OfType<StackPanel>().Single(p => p.Name == "SshFields");
            Assert.False(ssh.IsVisible);
            editor.UseSsh = true;
            Assert.True(ssh.IsVisible);
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Editor_Sqlite_HidesServerFields()
    {
        await Headless.Session.Dispatch(() =>
        {
            var editor = new ConnectionEditorViewModel(null, new NullStore(), new ProviderRegistry(), new Dialogs());
            var window = new ConnectionEditorWindow { DataContext = editor };
            var server = window.GetLogicalDescendants().OfType<StackPanel>().Single(p => p.Name == "ServerFields");
            var sqlite = window.GetLogicalDescendants().OfType<StackPanel>().Single(p => p.Name == "SqliteFields");
            Assert.True(server.IsVisible);
            Assert.False(sqlite.IsVisible);
            editor.Dialect = DatabaseDialect.Sqlite;
            Assert.False(server.IsVisible);
            Assert.True(sqlite.IsVisible);
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Editor_EngineTile_SetsDialect()
    {
        await Headless.Session.Dispatch(() =>
        {
            var editor = new ConnectionEditorViewModel(null, new NullStore(), new ProviderRegistry(), new Dialogs());
            var window = new ConnectionEditorWindow { DataContext = editor };
            var tile = window.FindControl<Button>("TileSqlServer")!;
            tile.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(DatabaseDialect.SqlServer, editor.Dialect);
            Assert.Contains("selected", tile.Classes);
            Assert.DoesNotContain("selected", window.FindControl<Button>("TilePostgres")!.Classes);
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Editor_EnvironmentTile_SetsEnvironment()
    {
        await Headless.Session.Dispatch(() =>
        {
            var editor = new ConnectionEditorViewModel(null, new NullStore(), new ProviderRegistry(), new Dialogs());
            var window = new ConnectionEditorWindow { DataContext = editor };
            var tile = window.FindControl<Button>("EnvStaging")!;
            tile.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(ConnectionEnvironment.Staging, editor.Environment);
            Assert.Contains("selected", tile.Classes);
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Editor_SaveButton_EnabledOnlyWhenValid()
    {
        await Headless.Session.Dispatch(() =>
        {
            var editor = new ConnectionEditorViewModel(null, new NullStore(), new ProviderRegistry(), new Dialogs());
            var window = new ConnectionEditorWindow { DataContext = editor };
            var save = window.FindControl<Button>("SaveButton")!;
            var test = window.FindControl<Button>("TestButton")!;
            Assert.False(save.Command!.CanExecute(null));
            Assert.True(test.Command!.CanExecute(null));
            editor.Name = "pg";
            editor.Host = "localhost";
            Assert.True(save.Command!.CanExecute(null));
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Editor_List_GroupsFoldersFirstThenUngrouped()
    {
        await Headless.Session.Dispatch(async () =>
        {
            var store = new ListStore(
                Profile("zeta", null), Profile("beta", "Staging"), Profile("alpha", "Production"), Profile("gamma", "Production"));
            var list = new ConnectionListViewModel(store, new ProviderRegistry(), new Dialogs());
            await list.LoadCommand.ExecuteAsync(null);
            var window = new ConnectionEditorWindow
            {
                DataContext = new ConnectionEditorViewModel(null, store, new ProviderRegistry(), new Dialogs()),
                List = list,
            };
            var host = window.FindControl<StackPanel>("ListHost")!;
            var texts = host.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).Where(t => !string.IsNullOrEmpty(t)).ToArray();
            Assert.Equal(["Production", "2", "PG", "alpha", "PG", "gamma", "Staging", "1", "PG", "beta", "PG", "zeta"], texts);
        }, TestContext.Current.CancellationToken);
    }

    private static ConnectionProfile Profile(string name, string? folder) =>
        new(Guid.NewGuid(), name, DatabaseDialect.PostgreSql, "h", 5432, null, null, null, null, ConnectionEnvironment.None, folder);

    private sealed class ListStore(params ConnectionProfile[] profiles) : IProfileStore
    {
        public ValueTask<IReadOnlyList<ConnectionProfile>> ListAsync(CancellationToken ct) => ValueTask.FromResult<IReadOnlyList<ConnectionProfile>>(profiles);
        public ValueTask SaveAsync(ConnectionProfile profile, string? password, string? sshSecret, CancellationToken ct) => ValueTask.CompletedTask;
        public ValueTask DeleteAsync(Guid id, CancellationToken ct) => ValueTask.CompletedTask;
        public ValueTask<string?> GetPasswordAsync(Guid id, CancellationToken ct) => ValueTask.FromResult<string?>(null);
        public ValueTask<string?> GetSshSecretAsync(Guid id, CancellationToken ct) => ValueTask.FromResult<string?>(null);
    }

    [Fact]
    public async Task Sidebar_ContextMenuCommands_AreBound()
    {
        await Headless.Session.Dispatch(() =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "squill-ui-" + Guid.NewGuid().ToString("N"));
            var services = AppComposition.Create(new Platform(new ProviderRegistry()), new AppPaths(dir));
            var window = new MainWindow { DataContext = services.Workspace };
            // The menu lives in the node template, which headless never realizes: check what its handlers run.
            var explorer = window.GetLogicalDescendants().OfType<ObjectExplorerView>().Single();
            Assert.Same(services.Workspace, explorer.Workspace);
            Assert.NotNull(services.Workspace.ConnectionList.NewCommand);
            Assert.NotNull(services.Workspace.ConnectionList.EditCommand);
            Assert.NotNull(services.Workspace.ConnectionList.DeleteCommand);
            Assert.NotNull(services.Workspace.ConnectCommand);
            Assert.NotNull(services.Workspace.DisconnectCommand);
        }, TestContext.Current.CancellationToken);
    }

    private sealed class NullStore : IProfileStore
    {
        public ValueTask<IReadOnlyList<ConnectionProfile>> ListAsync(CancellationToken ct) => ValueTask.FromResult<IReadOnlyList<ConnectionProfile>>([]);
        public ValueTask SaveAsync(ConnectionProfile profile, string? password, string? sshSecret, CancellationToken ct) => ValueTask.CompletedTask;
        public ValueTask DeleteAsync(Guid id, CancellationToken ct) => ValueTask.CompletedTask;
        public ValueTask<string?> GetPasswordAsync(Guid id, CancellationToken ct) => ValueTask.FromResult<string?>(null);
        public ValueTask<string?> GetSshSecretAsync(Guid id, CancellationToken ct) => ValueTask.FromResult<string?>(null);
    }

    private sealed class Platform(ProviderRegistry providers) : IPlatformServices
    {
        public IUiDispatcher Dispatcher { get; } = new ImmediateDispatcher();
        public IDialogService Dialogs { get; } = new ConnectionUiTests.Dialogs();
        public Func<Squill.Presentation.Editor.ISqlEditor> EditorFactory { get; } = () => new Squill.App.Avalonia.Editors.AvaloniaSqlEditor();
        public ProviderRegistry Providers { get; } = providers;
    }

    private sealed class Dialogs : IDialogService
    {
        public ValueTask<bool> ConfirmAsync(string title, string message) => ValueTask.FromResult(true);
        public ValueTask<string?> PromptAsync(string title, string message, string? initial) => ValueTask.FromResult<string?>(null);
        public ValueTask<string?> PickSaveFileAsync(string suggestedName, string extension) => ValueTask.FromResult<string?>(null);
        public ValueTask ShowErrorAsync(string title, string message) => ValueTask.CompletedTask;
        public ValueTask<IReadOnlyList<ParameterValue>?> PromptParametersAsync(
            IReadOnlyList<string> names,
            IReadOnlyDictionary<string, ParameterValue> defaults) => ValueTask.FromResult<IReadOnlyList<ParameterValue>?>(null);
    }
}
