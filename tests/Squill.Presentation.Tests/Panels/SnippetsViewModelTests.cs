using Squill.Core;
using Squill.Platform.Storage;
using Squill.Presentation.Panels;
using Xunit;

namespace Squill.Presentation.Tests.Panels;

public sealed class SnippetsViewModelTests
{
    private readonly PanelsHost _host = new();
    private readonly FakeStore _store = new();
    private readonly SnippetsViewModel _snippets;

    public SnippetsViewModelTests() => _snippets = new SnippetsViewModel(_store, _host.Workspace, _host.Dialogs);

    [Fact]
    public async Task SaveCurrent_WholeText_SavesWithPromptedName()
    {
        await _host.ConnectAsync();
        _host.Editors[0].Text = "SELECT 1";
        _host.Dialogs.PromptResult = " one ";

        await _snippets.SaveCurrentCommand.ExecuteAsync(null);

        var saved = Assert.Single(_store.Items);
        Assert.Equal(("one", "SELECT 1"), (saved.Name, saved.Sql));
        Assert.Single(_snippets.Snippets);
    }

    [Fact]
    public async Task SaveCurrent_WithSelection_SavesSelectionOnly()
    {
        await _host.ConnectAsync();
        _host.Editors[0].Text = "SELECT 1; SELECT 2";
        _host.Editors[0].Selection = new TextRange(10, 8);
        _host.Dialogs.PromptResult = "two";

        await _snippets.SaveCurrentCommand.ExecuteAsync(null);

        Assert.Equal("SELECT 2", Assert.Single(_store.Items).Sql);
    }

    [Fact]
    public async Task SaveCurrent_NoTab_DoesNothing()
    {
        _host.Dialogs.PromptResult = "x";

        await _snippets.SaveCurrentCommand.ExecuteAsync(null);

        Assert.Empty(_store.Items);
    }

    [Fact]
    public async Task Insert_WithSelection_ReplacesSelection()
    {
        await _host.ConnectAsync();
        _host.Editors[0].Text = "A XX B";
        _host.Editors[0].Selection = new TextRange(2, 2);

        _snippets.InsertCommand.Execute(new Snippet(Guid.NewGuid(), "s", "SELECT 1"));

        Assert.Equal("A SELECT 1 B", _host.Editors[0].Text);
        Assert.Equal(10, _host.Editors[0].CaretOffset);
        Assert.Equal(0, _host.Editors[0].Selection.Length);
    }

    [Fact]
    public async Task Insert_NoSelection_InsertsAtCaret()
    {
        await _host.ConnectAsync();
        _host.Editors[0].Text = "AB";
        _host.Editors[0].Selection = new TextRange(1, 0);

        _snippets.InsertCommand.Execute(new Snippet(Guid.NewGuid(), "s", "-"));

        Assert.Equal("A-B", _host.Editors[0].Text);
    }

    [Fact]
    public async Task Delete_Confirmed_RemovesSnippet()
    {
        var snippet = new Snippet(Guid.NewGuid(), "s", "SELECT 1");
        _store.Items.Add(snippet);
        await _snippets.LoadCommand.ExecuteAsync(null);
        _host.Dialogs.ConfirmResult = false;

        await _snippets.DeleteCommand.ExecuteAsync(snippet);
        Assert.Single(_snippets.Snippets);

        _host.Dialogs.ConfirmResult = true;
        await _snippets.DeleteCommand.ExecuteAsync(snippet);
        Assert.Empty(_snippets.Snippets);
        Assert.Empty(_store.Items);
    }

    [Fact]
    public async Task Rename_NewName_UpdatesSnippet()
    {
        var snippet = new Snippet(Guid.NewGuid(), "old", "SELECT 1");
        _store.Items.Add(snippet);
        await _snippets.LoadCommand.ExecuteAsync(null);
        _host.Dialogs.PromptResult = "new";

        await _snippets.RenameCommand.ExecuteAsync(snippet);

        Assert.Equal("new", Assert.Single(_snippets.Snippets).Name);
        Assert.Equal("SELECT 1", _snippets.Snippets[0].Sql);
    }

    private sealed class FakeStore : ISnippetStore
    {
        public List<Snippet> Items { get; } = [];

        public ValueTask<IReadOnlyList<Snippet>> ListAsync(CancellationToken ct) => ValueTask.FromResult<IReadOnlyList<Snippet>>([.. Items]);

        public ValueTask SaveAsync(Snippet snippet, CancellationToken ct)
        {
            var i = Items.FindIndex(s => s.Id == snippet.Id);
            if (i >= 0)
                Items[i] = snippet;
            else
                Items.Add(snippet);
            return ValueTask.CompletedTask;
        }

        public ValueTask DeleteAsync(Guid id, CancellationToken ct)
        {
            Items.RemoveAll(s => s.Id == id);
            return ValueTask.CompletedTask;
        }
    }
}
