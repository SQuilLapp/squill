using Squill.Core;
using Squill.Core.Editing;
using Squill.Data;
using Squill.Presentation.Editing;
using Squill.Presentation.Results;
using Squill.Presentation.Sessions;
using Xunit;

namespace Squill.Presentation.Tests.Editing;

public sealed class ResultEditorViewModelTests
{
    private static readonly ColumnDefinition[] Defs =
    [
        new("Id", "int", false, true, null, true),
        new("Name", "text", false, false, null),
        new("Note", "text", true, false, null),
        new("Qty", "int", false, false, "0"),
    ];

    private static readonly ColumnInfo[] Cols =
    [
        new("Id", "int", typeof(int), false, 0),
        new("Name", "text", typeof(string), false, 1),
        new("Note", "text", typeof(string), true, 2),
        new("Qty", "int", typeof(int), false, 3),
        new("Computed", "int", typeof(int), true, 4),
    ];

    private static readonly EditTarget Target =
        new(null, "T", [.. Defs.Select((d, i) => new EditColumn(i, d))], ["Id"], new TableInfo(null, "T", TableKind.Table, Defs, [], []));

    private sealed class Fake(int rows, bool complete = true) : IGridDataSource
    {
        public event EventHandler? Changed
        {
            add { }
            remove { }
        }

        public IReadOnlyList<ColumnInfo> Columns => Cols;
        public long RowCount => rows;
        public bool IsComplete => complete;
        public bool IsTruncated => false;

        public object? GetCell(long row, int column) => column switch
        {
            0 => (int)row,
            1 => "n" + row,
            2 => "t" + row,
            _ => 1,
        };
    }

    private sealed class Reader(object?[][] rows, long affected) : IResultReader
    {
        private bool _done;
        public IReadOnlyList<ColumnInfo> Columns => [];
        public long RecordsAffected => affected;

        public ValueTask<object?[][]?> ReadBatchAsync(int maxRows, CancellationToken cancellationToken)
        {
            if (_done)
                return ValueTask.FromResult<object?[][]?>(null);
            _done = true;
            return ValueTask.FromResult<object?[][]?>(rows);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class Conn : IDatabaseConnection
    {
        public List<string> Sql { get; } = [];
        public Queue<long> Affected { get; } = new();
        public Queue<object?[][]> Rows { get; } = new();
        public string? FailOn { get; set; }
        public TaskCompletionSource? Gate { get; set; }
        public TaskCompletionSource Started { get; } = new();
        public string ServerVersion => "1";

        public async ValueTask<IResultReader> ExecuteAsync(string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken)
        {
            Sql.Add(sql);
            if (Gate is not null && sql != "ROLLBACK")
            {
                Started.TrySetResult();
                await Gate.Task.WaitAsync(cancellationToken);
            }

            if (FailOn is not null && sql.Contains(FailOn, StringComparison.Ordinal))
                throw new InvalidOperationException("boom");
            var dml = sql.StartsWith("UPDATE", StringComparison.Ordinal) || sql.StartsWith("DELETE", StringComparison.Ordinal);
            var isKey = sql.Contains("RETURNING", StringComparison.Ordinal);
            return new Reader(isKey && Rows.Count > 0 ? Rows.Dequeue() : [], dml ? (Affected.Count > 0 ? Affected.Dequeue() : 1) : -1);
        }

        public ValueTask<SchemaSnapshot> ReadSchemaAsync(CancellationToken cancellationToken) => ValueTask.FromResult(SchemaSnapshot.Empty);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class Fixture
    {
        public Fixture(int rows = 5, bool complete = true)
        {
            Grid = new EditableGridDataSource(new Fake(rows, complete), Target);
            Session = new ConnectionSession(
                new ConnectionProfile(Guid.NewGuid(), "p", DatabaseDialect.Sqlite, "h", 1, "d", "u", null, null),
                Connection, SchemaSnapshot.Empty, null);
            Vm = new ResultEditorViewModel(Grid, Target, Session, Dialogs, () => Busy);
        }

        public bool Busy { get; set; }
        public Conn Connection { get; } = new();
        public FakeDialogService Dialogs { get; } = new();
        public EditableGridDataSource Grid { get; }
        public ConnectionSession Session { get; }
        public ResultEditorViewModel Vm { get; }
    }

    [Fact]
    public void TrySetCell_ParsesAndRecords()
    {
        var f = new Fixture();

        Assert.Null(f.Vm.TrySetCell(1, 3, " 42 "));
        Assert.Equal(42, f.Grid.GetCell(1, 3));
        Assert.True(f.Vm.HasPendingChanges);
        Assert.Equal(1, f.Vm.PendingCount);
        Assert.Equal("1 pending change(s)", f.Vm.StatusText);
    }

    [Fact]
    public void TrySetCell_ParseErrorLeavesGridUnchanged()
    {
        var f = new Fixture();

        var error = f.Vm.TrySetCell(1, 3, "abc");

        Assert.Contains("not a valid integer", error, StringComparison.Ordinal);
        Assert.False(f.Vm.HasPendingChanges);
        Assert.Equal(1, f.Grid.GetCell(1, 3));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(99)]
    public void TrySetCell_ColumnOutsideTargetIsReadOnly(int column)
    {
        var f = new Fixture();

        Assert.Equal("This column is read-only", f.Vm.TrySetCell(0, column, "1"));
    }

    [Fact]
    public void TrySetCell_RefusesDeletedRowAndLoadingRows()
    {
        var f = new Fixture();
        f.Vm.DeleteRows([2]);
        Assert.Equal("Row is marked for deletion", f.Vm.TrySetCell(2, 1, "x"));

        var loading = new Fixture(complete: false);
        Assert.Equal("Rows are still loading", loading.Vm.TrySetCell(0, 1, "x"));
    }

    [Fact]
    public void SetNull_StoresNullInTextColumn()
    {
        var f = new Fixture();

        f.Vm.SetNull(0, 2);

        Assert.Null(f.Grid.GetCell(0, 2));
        Assert.True(f.Grid.IsCellModified(0, 2));
    }

    [Fact]
    public void AddDeleteRevert_WorkWithPendingInsertedRows()
    {
        var f = new Fixture();
        var a = f.Vm.AddRow();
        var b = f.Vm.AddRow();
        Assert.Equal([5L, 6L], [a, b]);

        f.Vm.DeleteRows([1, a, a, b]);
        Assert.Equal(5, f.Grid.RowCount);
        Assert.Equal(RowState.Deleted, f.Grid.GetRowState(1));
        Assert.Equal(1, f.Vm.PendingCount);

        f.Vm.RevertRows([1, 3, 1]);
        Assert.Equal(RowState.Unchanged, f.Grid.GetRowState(1));
        Assert.False(f.Vm.HasPendingChanges);
        Assert.Equal("", f.Vm.StatusText);
    }

    [Fact]
    public void Issues_RefreshAfterEveryEdit()
    {
        var f = new Fixture();

        var row = f.Vm.AddRow();
        var issue = Assert.Single(f.Vm.Issues);
        Assert.Equal((row, "Name"), (issue.Row, issue.Column));
        Assert.False(f.Vm.CommitCommand.CanExecute(null));
        Assert.False(f.Vm.PreviewScriptCommand.CanExecute(null));

        Assert.Null(f.Vm.TrySetCell(row, 1, "x"));
        Assert.Empty(f.Vm.Issues);
        Assert.True(f.Vm.CommitCommand.CanExecute(null));
    }

    [Fact]
    public async Task Commit_SuccessClearsPendingAndShowsGeneratedKey()
    {
        var f = new Fixture();
        var row = f.Vm.AddRow();
        f.Vm.TrySetCell(row, 1, "x");
        f.Vm.TrySetCell(0, 2, "u");
        f.Connection.Rows.Enqueue([[77L]]);

        await f.Vm.CommitCommand.ExecuteAsync(null);

        Assert.Equal("Committed 2 change(s)", f.Vm.StatusText);
        Assert.False(f.Vm.HasPendingChanges);
        Assert.Empty(f.Vm.Issues);
        Assert.Equal(77, f.Grid.GetCell(row, 0));
        Assert.False(f.Vm.IsCommitting);
        Assert.Empty(f.Dialogs.Errors);
    }

    [Fact]
    public async Task Commit_BlockedWhileConnectionBusy()
    {
        var f = new Fixture();
        f.Vm.TrySetCell(0, 1, "x");
        f.Busy = true;

        await f.Vm.CommitCommand.ExecuteAsync(null);

        Assert.Equal(("Commit blocked", "A query is still running on this connection."), Assert.Single(f.Dialogs.Errors));
        Assert.Empty(f.Connection.Sql);
        Assert.True(f.Vm.HasPendingChanges);
    }

    [Fact]
    public async Task Commit_DeleteConfirmationDeclinedDoesNothing()
    {
        var f = new Fixture();
        f.Vm.DeleteRows([0, 1]);
        f.Dialogs.ConfirmResult = false;

        await f.Vm.CommitCommand.ExecuteAsync(null);

        var (title, message) = Assert.Single(f.Dialogs.Confirms);
        Assert.Equal("Delete rows", title);
        Assert.StartsWith("Delete 2 row(s) from ", message, StringComparison.Ordinal);
        Assert.EndsWith("? This cannot be undone.", message, StringComparison.Ordinal);
        Assert.Empty(f.Connection.Sql);
        Assert.True(f.Vm.HasPendingChanges);
    }

    [Fact]
    public async Task Commit_DeleteConfirmationAcceptedCommits()
    {
        var f = new Fixture();
        f.Vm.DeleteRows([0]);

        await f.Vm.CommitCommand.ExecuteAsync(null);

        Assert.Contains(f.Connection.Sql, s => s.StartsWith("DELETE", StringComparison.Ordinal));
        Assert.Equal("Committed 1 change(s)", f.Vm.StatusText);
        Assert.False(f.Vm.HasPendingChanges);
        Assert.Equal(4, f.Grid.RowCount);
    }

    [Fact]
    public async Task Commit_ProviderFailureKeepsChangesAndReportsRow()
    {
        var f = new Fixture();
        f.Vm.TrySetCell(3, 1, "x");
        f.Connection.FailOn = "UPDATE";

        await f.Vm.CommitCommand.ExecuteAsync(null);

        Assert.Equal(("Commit failed", "boom"), Assert.Single(f.Dialogs.Errors));
        Assert.Equal("boom", f.Vm.StatusText);
        var issue = Assert.Single(f.Vm.Issues);
        Assert.Equal((3L, null), (issue.Row, issue.Column));
        Assert.True(f.Vm.HasPendingChanges);
        Assert.False(f.Vm.CommitCommand.CanExecute(null));

        f.Vm.TrySetCell(3, 1, "y");
        Assert.Empty(f.Vm.Issues);
    }

    [Fact]
    public async Task Cancel_AbortsRunningCommitAndKeepsChanges()
    {
        var f = new Fixture();
        f.Vm.TrySetCell(0, 1, "x");
        f.Connection.Gate = new TaskCompletionSource();

        var commit = f.Vm.CommitCommand.ExecuteAsync(null);
        await f.Connection.Started.Task;
        Assert.True(f.Vm.IsCommitting);
        f.Vm.CancelCommand.Execute(null);
        await commit;

        Assert.Equal("Cancelled", f.Vm.StatusText);
        Assert.False(f.Vm.IsCommitting);
        Assert.True(f.Vm.HasPendingChanges);
        Assert.Empty(f.Dialogs.Errors);
    }

    [Fact]
    public async Task Discard_WithoutChangesDoesNotAsk()
    {
        var f = new Fixture();

        await f.Vm.DiscardCommand.ExecuteAsync(null);

        Assert.Empty(f.Dialogs.Confirms);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Discard_AsksThenDrops(bool confirm, bool stillPending)
    {
        var f = new Fixture();
        f.Vm.TrySetCell(0, 1, "x");
        f.Vm.AddRow();
        f.Dialogs.ConfirmResult = confirm;

        await f.Vm.DiscardCommand.ExecuteAsync(null);

        Assert.Equal(("Discard changes", "Discard 2 pending change(s)?"), Assert.Single(f.Dialogs.Confirms));
        Assert.Equal(stillPending, f.Vm.HasPendingChanges);
    }

    [Fact]
    public void PreviewScript_ListsEachEditKindWithParameters()
    {
        var f = new Fixture();
        f.Vm.DeleteRows([4]);
        f.Vm.TrySetCell(1, 1, "it's");
        f.Vm.SetNull(1, 2);
        var row = f.Vm.AddRow();
        f.Vm.TrySetCell(row, 1, "new");
        f.Vm.TrySetCell(row, 3, "7");
        string? script = null;
        f.Vm.ScriptRequested += s => script = s;

        f.Vm.PreviewScriptCommand.Execute(null);

        Assert.NotNull(script);
        var blocks = script.Split("\n\n");
        Assert.Equal(3, blocks.Length);
        Assert.StartsWith("DELETE", blocks[0], StringComparison.Ordinal);
        Assert.EndsWith(";\n-- @p0 = 4", blocks[0], StringComparison.Ordinal);
        Assert.StartsWith("UPDATE", blocks[1], StringComparison.Ordinal);
        Assert.EndsWith(";\n-- @p0 = 'it''s', @p1 = NULL, @p2 = 1", blocks[1], StringComparison.Ordinal);
        Assert.StartsWith("INSERT", blocks[2], StringComparison.Ordinal);
        Assert.EndsWith(";\n-- @p0 = 'new', @p1 = 7", blocks[2], StringComparison.Ordinal);
    }

    [Fact]
    public void PreviewScript_RaisesNothingWithoutChangesOrWithIssues()
    {
        var f = new Fixture();
        var raised = 0;
        f.Vm.ScriptRequested += _ => raised++;

        f.Vm.PreviewScriptCommand.Execute(null);
        f.Vm.AddRow();
        f.Vm.PreviewScriptCommand.Execute(null);

        Assert.Equal(0, raised);
    }
}
