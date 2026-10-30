using Squill.Platform.Storage;
using Squill.Presentation.Panels;
using Xunit;

namespace Squill.Presentation.Tests.Panels;

public sealed class HistoryViewModelTests
{
    private readonly PanelsHost _host = new();
    private readonly FakeStore _store = new();
    private readonly HistoryViewModel _history;

    public HistoryViewModelTests() => _history = new HistoryViewModel(_store, _host.Workspace, _host.Dialogs);

    private static HistoryEntry Entry(string sql, int minute) =>
        new(new DateTimeOffset(2026, 1, 1, 0, minute, 0, TimeSpan.Zero), Guid.Empty, sql, true, 1, TimeSpan.Zero);

    [Fact]
    public async Task Load_StoreReturnsOldestFirst_ShowsNewestFirst()
    {
        _store.Entries.AddRange([Entry("SELECT 1", 1), Entry("SELECT 2", 2)]);

        await _history.LoadCommand.ExecuteAsync(null);

        Assert.Equal(["SELECT 2", "SELECT 1"], _history.Entries.Select(e => e.Sql));
        Assert.Equal(500, _store.LastMax);
    }

    [Fact]
    public async Task FilterText_Set_FiltersCaseInsensitively()
    {
        _store.Entries.AddRange([Entry("select * from Users", 1), Entry("SELECT 1", 2)]);
        await _history.LoadCommand.ExecuteAsync(null);

        _history.FilterText = "USERS";

        Assert.Equal("select * from Users", Assert.Single(_history.Entries).Sql);

        _history.FilterText = "";
        Assert.Equal(2, _history.Entries.Count);
    }

    [Fact]
    public async Task Clear_Confirmed_EmptiesStoreAndList()
    {
        _store.Entries.Add(Entry("SELECT 1", 1));
        await _history.LoadCommand.ExecuteAsync(null);
        _host.Dialogs.ConfirmResult = false;

        await _history.ClearCommand.ExecuteAsync(null);
        Assert.Single(_history.Entries);

        _host.Dialogs.ConfirmResult = true;
        await _history.ClearCommand.ExecuteAsync(null);
        Assert.Empty(_history.Entries);
        Assert.Empty(_store.Entries);
    }

    [Fact]
    public async Task Open_WithSession_OpensTabWithSqlWithoutRunning()
    {
        await _host.ConnectAsync();

        _history.OpenCommand.Execute(Entry("SELECT 42", 1));

        Assert.Equal(2, _host.Workspace.Tabs.Count);
        Assert.Equal("SELECT 42", _host.Editors[1].Text);
        Assert.Same(_host.Workspace.Tabs[1], _host.Workspace.ActiveTab);
    }

    [Fact]
    public void Open_NoSession_DoesNothing()
    {
        _history.OpenCommand.Execute(Entry("SELECT 42", 1));

        Assert.Empty(_host.Workspace.Tabs);
    }

    private sealed class FakeStore : IQueryHistoryStore
    {
        public List<HistoryEntry> Entries { get; } = [];
        public int LastMax { get; private set; }

        public ValueTask AppendAsync(HistoryEntry e, CancellationToken ct)
        {
            Entries.Add(e);
            return ValueTask.CompletedTask;
        }

        public ValueTask<IReadOnlyList<HistoryEntry>> ListAsync(int max, CancellationToken ct)
        {
            LastMax = max;
            return ValueTask.FromResult<IReadOnlyList<HistoryEntry>>([.. Entries]);
        }

        public ValueTask ClearAsync(CancellationToken ct)
        {
            Entries.Clear();
            return ValueTask.CompletedTask;
        }
    }
}
