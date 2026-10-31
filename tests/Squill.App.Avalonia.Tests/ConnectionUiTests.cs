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
    public async Task Sidebar_ContextMenuCommands_AreBound()
    {
        await Headless.Session.Dispatch(() =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "squill-ui-" + Guid.NewGuid().ToString("N"));
            var services = AppComposition.Create(new Platform(new ProviderRegistry()), new AppPaths(dir));
            var window = new MainWindow { DataContext = services.Workspace };
            var list = window.GetLogicalDescendants().OfType<ListBox>().Single(l => l.Name == "Profiles");
            Assert.NotNull(list.ContextMenu);
            // Headless has no popup host, so the items get the data context the open menu would give them.
            foreach (var item in list.ContextMenu!.Items.OfType<MenuItem>())
            {
                item.DataContext = services.Workspace;
                Assert.NotNull(item.Command);
            }
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
