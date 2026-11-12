using Squill.Core;
using Squill.Core.Editing;
using Squill.Core.Transactions;
using Squill.Data;
using Squill.Presentation.Editing;
using Squill.Presentation.Localization;
using Squill.Presentation.Sessions;
using Squill.Sql.Transactions;
using Xunit;

namespace Squill.Presentation.Tests.Editing;

public sealed class EditCommitterTransactionTests
{
    private static readonly ColumnDefinition[] Defs = [new("Id", "int", false, true, null, true), new("Name", "text", false, false, null)];

    private static readonly EditTarget Target =
        new(null, "T", [.. Defs.Select((d, i) => new EditColumn(i, d))], ["Id"], new TableInfo(null, "T", TableKind.Table, Defs, [], []));

    private static readonly ColumnInfo[] Cols = [new("Id", "int", typeof(int), false, 0), new("Name", "text", typeof(string), false, 1)];

    private sealed class Reader(long affected) : IResultReader
    {
        public IReadOnlyList<ColumnInfo> Columns => [];
        public long RecordsAffected => affected;
        public ValueTask<object?[][]?> ReadBatchAsync(int maxRows, CancellationToken cancellationToken) => ValueTask.FromResult<object?[][]?>(null);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class Conn : IDatabaseConnection
    {
        public List<string> Sql { get; } = [];
        public Func<string, Exception?>? Fail { get; set; }
        public string ServerVersion => "1";

        public ValueTask<IResultReader> ExecuteAsync(string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken)
        {
            Sql.Add(sql);
            if (Fail?.Invoke(sql) is { } ex)
                throw ex;
            return ValueTask.FromResult<IResultReader>(new Reader(sql.StartsWith("UPDATE", StringComparison.Ordinal) ? 1 : -1));
        }

        public ValueTask<IReadOnlyList<string>> ListDatabasesAsync(CancellationToken cancellationToken) => ValueTask.FromResult<IReadOnlyList<string>>([]);
        public ValueTask<SchemaSnapshot> ReadSchemaAsync(CancellationToken cancellationToken) => ValueTask.FromResult(SchemaSnapshot.Empty);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static (SessionTransaction Tx, Conn C) Make(DatabaseDialect d, bool open = true)
    {
        var c = new Conn();
        var tx = new SessionTransaction(c, d, TimeProvider.System);
        if (open)
            tx.ObserveSuccess("BEGIN", StatementClassifier.Classify("BEGIN", d), -1, "x");
        return (tx, c);
    }

    private static PendingEdit Upd(long row, int id) => new(row, new UpdateRow([new ColumnValue("Id", id)], [new ColumnValue("Name", "n")]));

    [Theory]
    [InlineData(DatabaseDialect.PostgreSql, "BEGIN", "COMMIT")]
    [InlineData(DatabaseDialect.SqlServer, "BEGIN TRANSACTION", "COMMIT TRANSACTION")]
    [InlineData(DatabaseDialect.Sqlite, "BEGIN", "COMMIT")]
    public async Task Without_a_transaction_the_commit_brackets_its_own_as_before(DatabaseDialect d, string begin, string commit)
    {
        var (tx, c) = Make(d, open: false);
        await new EditCommitter(c, d, tx, "Q1").CommitAsync(Target, Cols, [Upd(0, 1)], CancellationToken.None);
        Assert.Equal(begin, c.Sql[0]);
        Assert.Equal(commit, c.Sql[^1]);
        Assert.Equal(0, tx.PendingCount);
    }

    [Theory]
    [InlineData(DatabaseDialect.PostgreSql, "SAVEPOINT squill_sp_1", "RELEASE SAVEPOINT squill_sp_1")]
    [InlineData(DatabaseDialect.MySql, "SAVEPOINT squill_sp_1", "RELEASE SAVEPOINT squill_sp_1")]
    [InlineData(DatabaseDialect.Sqlite, "SAVEPOINT squill_sp_1", "RELEASE SAVEPOINT squill_sp_1")]
    [InlineData(DatabaseDialect.SqlServer, "SAVE TRANSACTION squill_sp_1", null)]
    public async Task Inside_an_open_transaction_it_uses_a_savepoint_and_counts_the_statements(DatabaseDialect d, string savepoint, string? release)
    {
        var (tx, c) = Make(d);
        var result = await new EditCommitter(c, d, tx, "Q1").CommitAsync(Target, Cols, [Upd(0, 1), Upd(1, 2)], CancellationToken.None);

        Assert.Equal(2, result.StatementCount);
        Assert.Equal(savepoint, c.Sql[0]);
        Assert.Equal(release is null ? 3 : 4, c.Sql.Count);
        if (release is not null)
            Assert.Equal(release, c.Sql[^1]);
        Assert.DoesNotContain(c.Sql, s => s.StartsWith("BEGIN", StringComparison.Ordinal) || s.StartsWith("START", StringComparison.Ordinal) || s.StartsWith("COMMIT", StringComparison.Ordinal));
        Assert.Equal(2, tx.PendingCount);
        Assert.All(tx.Journal, j => Assert.Equal("Q1", j.Tab));
        Assert.Equal(TransactionState.Open, tx.State);
        Assert.False(tx.IsBusy);
    }

    [Theory]
    [InlineData(DatabaseDialect.PostgreSql, "ROLLBACK TO SAVEPOINT squill_sp_1")]
    [InlineData(DatabaseDialect.SqlServer, "ROLLBACK TRANSACTION squill_sp_1")]
    public async Task A_failing_edit_rolls_back_to_the_savepoint_only(DatabaseDialect d, string rollbackTo)
    {
        var (tx, c) = Make(d);
        tx.ObserveSuccess("UPDATE earlier SET a = 1 WHERE b = 2", StatementClassifier.Classify("UPDATE earlier SET a = 1 WHERE b = 2", d), 1, "x");
        c.Fail = sql => sql.StartsWith("UPDATE", StringComparison.Ordinal) ? new InvalidOperationException("boom") : null;

        await Assert.ThrowsAsync<EditCommitException>(() => new EditCommitter(c, d, tx, "Q1").CommitAsync(Target, Cols, [Upd(0, 1)], CancellationToken.None));

        Assert.Equal(rollbackTo, c.Sql[^1]);
        Assert.DoesNotContain("ROLLBACK", c.Sql);
        Assert.Equal(TransactionState.Open, tx.State);
        Assert.Equal(1, tx.PendingCount);
    }

    [Fact]
    public async Task A_failed_transaction_refuses_the_commit_and_sends_nothing()
    {
        var (tx, c) = Make(DatabaseDialect.PostgreSql);
        await tx.ObserveFailureAsync(new InvalidOperationException(), StatementClassifier.Classify("UPDATE t SET a = 1", DatabaseDialect.PostgreSql), CancellationToken.None);

        var ex = await Assert.ThrowsAsync<EditCommitException>(() => new EditCommitter(c, DatabaseDialect.PostgreSql, tx).CommitAsync(Target, Cols, [Upd(0, 1)], CancellationToken.None));

        Assert.Empty(c.Sql);
        Assert.Equal(UiText.Get("tx.failed"), ex.Message);
    }

    [Fact]
    public async Task The_operation_counter_disables_the_transaction_buttons_during_the_commit()
    {
        var (tx, c) = Make(DatabaseDialect.PostgreSql);
        bool? canCommitDuring = null;
        c.Fail = _ =>
        {
            canCommitDuring ??= tx.CanCommit;
            return null;
        };

        await new EditCommitter(c, DatabaseDialect.PostgreSql, tx).CommitAsync(Target, Cols, [Upd(0, 1)], CancellationToken.None);

        Assert.False(canCommitDuring);
        Assert.True(tx.CanCommit);
    }
}
