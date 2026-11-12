using Squill.Core;
using Squill.Core.Transactions;
using Squill.Data;
using Squill.Presentation.Localization;
using Squill.Presentation.Sessions;
using Squill.Sql.Transactions;
using Xunit;

namespace Squill.Presentation.Tests.Sessions;

public sealed class SessionTransactionTests
{
    private sealed class Reader : IResultReader
    {
        public IReadOnlyList<ColumnInfo> Columns => [];
        public long RecordsAffected => 0;
        public ValueTask<object?[][]?> ReadBatchAsync(int maxRows, CancellationToken cancellationToken) => ValueTask.FromResult<object?[][]?>(null);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private class Connection : IDatabaseConnection
    {
        public List<string> Executed { get; } = [];
        public Func<string, Exception?> Fail { get; set; } = _ => null;
        public string ServerVersion => "1";

        public ValueTask<IResultReader> ExecuteAsync(string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken)
        {
            Executed.Add(sql);
            if (Fail(sql) is { } ex)
                throw ex;
            return ValueTask.FromResult<IResultReader>(new Reader());
        }

        public ValueTask<IReadOnlyList<string>> ListDatabasesAsync(CancellationToken cancellationToken) => ValueTask.FromResult<IReadOnlyList<string>>([]);
        public ValueTask<SchemaSnapshot> ReadSchemaAsync(CancellationToken cancellationToken) => ValueTask.FromResult(SchemaSnapshot.Empty);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class ProbedConnection : Connection, ITransactionProbe
    {
        public int Probes { get; private set; }
        public TransactionProbeResult Answer { get; set; } = TransactionProbeResult.Unknown;

        public ValueTask<TransactionProbeResult> ProbeAsync(CancellationToken cancellationToken)
        {
            Probes++;
            return ValueTask.FromResult(Answer);
        }
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static readonly CancellationToken Ct = CancellationToken.None;

    private static (SessionTransaction Tx, ProbedConnection C, Clock Time) Make(DatabaseDialect d = DatabaseDialect.PostgreSql)
    {
        var c = new ProbedConnection();
        var time = new Clock();
        return (new SessionTransaction(c, d, time), c, time);
    }

    private static StatementTraits T(string sql, DatabaseDialect d = DatabaseDialect.PostgreSql) => StatementClassifier.Classify(sql, d);

    private static async Task<SessionTransaction> OpenManual(SessionTransaction tx)
    {
        await tx.SetModeAsync(TransactionMode.Manual, Ct);
        await tx.EnsureOpenAsync(T("UPDATE t SET a = 1"), Ct);
        return tx;
    }

    [Fact]
    public async Task Lazy_begin_only_for_a_statement_that_is_not_read_only()
    {
        var (tx, c, _) = Make();
        await tx.SetModeAsync(TransactionMode.Manual, Ct);

        await tx.EnsureOpenAsync(T("SELECT 1"), Ct);
        Assert.Equal(TransactionState.None, tx.State);
        Assert.Empty(c.Executed);

        await tx.EnsureOpenAsync(T("DELETE FROM t WHERE a = 1"), Ct);
        Assert.Equal(TransactionState.Open, tx.State);
        Assert.Equal(["BEGIN"], c.Executed);
        Assert.NotNull(tx.OpenedAt);
    }

    [Fact]
    public async Task No_begin_in_auto_commit_mode()
    {
        var (tx, c, _) = Make();
        await tx.EnsureOpenAsync(T("DELETE FROM t"), Ct);
        Assert.Empty(c.Executed);
    }

    [Theory]
    [InlineData(DatabaseDialect.MySql, "START TRANSACTION", "COMMIT", "ROLLBACK")]
    [InlineData(DatabaseDialect.SqlServer, "BEGIN TRANSACTION", "COMMIT TRANSACTION", "ROLLBACK TRANSACTION")]
    [InlineData(DatabaseDialect.Sqlite, "BEGIN", "COMMIT", "ROLLBACK")]
    public async Task Begin_commit_and_rollback_text_follow_the_dialect(DatabaseDialect d, string begin, string commit, string rollback)
    {
        var (tx, c, _) = Make(d);
        await OpenManual(tx);
        await tx.CommitAsync(Ct);
        await OpenManual(tx);
        await tx.RollbackAsync(Ct);
        Assert.Equal([begin, commit, begin, rollback], c.Executed);
        Assert.Equal(TransactionState.None, tx.State);
    }

    [Fact]
    public async Task Successful_statements_are_counted_and_journaled_but_reads_are_not()
    {
        var (tx, _, time) = Make();
        await OpenManual(tx);
        tx.ObserveSuccess("UPDATE t SET a = 1", T("UPDATE t SET a = 1"), 3, "tab 1");
        tx.ObserveSuccess("SELECT 1", T("SELECT 1"), -1, "tab 1");

        Assert.Equal(1, tx.PendingCount);
        var entry = Assert.Single(tx.Journal);
        Assert.Equal(new PendingStatement("UPDATE t SET a = 1", "tab 1", time.Now, 3), entry);
    }

    [Fact]
    public async Task Journal_is_cut_at_500_characters_and_200_entries_while_the_count_goes_on()
    {
        var (tx, _, _) = Make();
        await OpenManual(tx);
        var traits = T("UPDATE t SET a = 1");
        tx.ObserveSuccess("UPDATE " + new string('x', 700), traits, 1, "t");
        Assert.Equal(500, tx.Journal[0].Text.Length);

        for (var i = 0; i < 250; i++)
            tx.ObserveSuccess("UPDATE t SET a = " + i, traits, 1, "t");

        Assert.Equal(251, tx.PendingCount);
        Assert.Equal(200, tx.Journal.Count);
        Assert.Equal("UPDATE t SET a = 249", tx.Journal[^1].Text);
    }

    [Theory]
    [InlineData("BEGIN")]
    [InlineData("START TRANSACTION")]
    [InlineData("BEGIN TRANSACTION")]
    public void A_typed_begin_opens_the_transaction_in_auto_commit_mode(string sql)
    {
        var (tx, _, _) = Make(DatabaseDialect.MySql);
        tx.ObserveSuccess(sql, T(sql, DatabaseDialect.MySql), -1, "t");
        Assert.Equal(TransactionState.Open, tx.State);
        Assert.Equal(TransactionMode.AutoCommit, tx.Mode);
        Assert.Equal(0, tx.PendingCount);
    }

    [Theory]
    [InlineData("COMMIT")]
    [InlineData("ROLLBACK")]
    [InlineData("END")]
    public async Task A_typed_commit_or_rollback_closes_the_transaction(string sql)
    {
        var (tx, _, _) = Make();
        await OpenManual(tx);
        tx.ObserveSuccess("UPDATE t SET a = 1", T("UPDATE t SET a = 1"), 1, "t");
        tx.ObserveSuccess(sql, T(sql), -1, "t");
        Assert.Equal(TransactionState.None, tx.State);
        Assert.Equal(0, tx.PendingCount);
        Assert.Empty(tx.Journal);
    }

    [Fact]
    public async Task Savepoint_statements_change_nothing_and_are_not_counted()
    {
        var (tx, _, _) = Make();
        await OpenManual(tx);
        tx.ObserveSuccess("SAVEPOINT a", T("SAVEPOINT a"), -1, "t");
        Assert.Equal(TransactionState.Open, tx.State);
        Assert.Equal(0, tx.PendingCount);
    }

    [Fact]
    public async Task Mysql_implicit_commit_ends_the_transaction()
    {
        var (tx, _, _) = Make(DatabaseDialect.MySql);
        await OpenManual(tx);
        tx.ObserveSuccess("UPDATE t SET a = 1", T("UPDATE t SET a = 1", DatabaseDialect.MySql), 1, "t");
        var ddl = T("ALTER TABLE t ADD c INT", DatabaseDialect.MySql);
        Assert.True(ddl.ImplicitCommit);
        tx.ObserveSuccess("ALTER TABLE t ADD c INT", ddl, 0, "t");
        Assert.Equal(TransactionState.None, tx.State);
        Assert.Equal(0, tx.PendingCount);
    }

    [Fact]
    public async Task Postgres_error_fails_the_transaction_and_rollback_clears_it_while_commit_is_refused()
    {
        var (tx, c, _) = Make();
        await OpenManual(tx);
        await tx.ObserveFailureAsync(new InvalidOperationException("x"), T("UPDATE t SET a = 1"), Ct);

        Assert.Equal(TransactionState.Failed, tx.State);
        Assert.Equal(0, tx.PendingCount);
        Assert.Equal(0, c.Probes);
        Assert.False(tx.CanCommit);
        Assert.True(tx.CanRollback);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => tx.CommitAsync(Ct).AsTask());
        Assert.Equal(UiText.Get("tx.failed"), ex.Message);

        tx.ObserveSuccess("ROLLBACK TO SAVEPOINT a", T("ROLLBACK TO SAVEPOINT a"), -1, "t");
        Assert.Equal(TransactionState.Open, tx.State);
        await tx.ObserveFailureAsync(new InvalidOperationException("x"), T("UPDATE t SET a = 1"), Ct);
        await tx.RollbackAsync(Ct);
        Assert.Equal(TransactionState.None, tx.State);
    }

    [Fact]
    public async Task Other_dialect_error_probes_the_server()
    {
        var (tx, c, _) = Make(DatabaseDialect.SqlServer);
        await OpenManual(tx);
        c.Answer = new(true, false);
        await tx.ObserveFailureAsync(new InvalidOperationException("x"), T("UPDATE t SET a = 1", DatabaseDialect.SqlServer), Ct);
        Assert.Equal(1, c.Probes);
        Assert.Equal(TransactionState.Open, tx.State);
    }

    [Fact]
    public async Task A_probe_that_says_the_transaction_ended_closes_it_and_raises_the_event()
    {
        var (tx, c, _) = Make(DatabaseDialect.MySql);
        await OpenManual(tx);
        string? reason = null;
        tx.ExternallyEnded += (_, r) => reason = r;
        c.Answer = new(false, false);

        await tx.ResyncAsync(Ct);

        Assert.Equal(TransactionState.None, tx.State);
        Assert.Equal(UiText.Get("tx.endedByServer"), reason);
    }

    [Fact]
    public async Task Probe_unknown_changes_nothing_and_probe_open_reopens_and_failed_fails()
    {
        var (tx, c, _) = Make();
        await tx.ResyncAsync(Ct);
        Assert.Equal(TransactionState.None, tx.State);

        c.Answer = new(true, false);
        await tx.ResyncAsync(Ct);
        Assert.Equal(TransactionState.Open, tx.State);

        c.Answer = TransactionProbeResult.Unknown;
        await tx.ResyncAsync(Ct);
        Assert.Equal(TransactionState.Open, tx.State);

        c.Answer = new(true, true);
        await tx.ResyncAsync(Ct);
        Assert.Equal(TransactionState.Failed, tx.State);
    }

    [Fact]
    public async Task Commit_failure_keeps_the_state_probes_and_rethrows()
    {
        var (tx, c, _) = Make();
        await OpenManual(tx);
        c.Fail = sql => sql == "COMMIT" ? new InvalidOperationException("boom") : null;
        c.Answer = new(true, false);
        var probesBefore = c.Probes;

        await Assert.ThrowsAsync<InvalidOperationException>(() => tx.CommitAsync(Ct).AsTask());

        Assert.Equal(TransactionState.Open, tx.State);
        Assert.Equal(probesBefore + 2, c.Probes);
    }

    [Fact]
    public async Task Auto_commit_is_refused_while_a_transaction_is_open()
    {
        var (tx, _, _) = Make();
        await OpenManual(tx);
        await Assert.ThrowsAsync<InvalidOperationException>(() => tx.SetModeAsync(TransactionMode.AutoCommit, Ct).AsTask());
        Assert.Equal(TransactionMode.Manual, tx.Mode);
    }

    [Fact]
    public async Task A_held_operation_makes_the_session_busy_and_commit_throws()
    {
        var (tx, _, _) = Make();
        await OpenManual(tx);
        var scope = tx.BeginOperation();
        Assert.True(tx.IsBusy);
        Assert.False(tx.CanCommit);
        Assert.False(tx.CanRollback);
        await Assert.ThrowsAsync<InvalidOperationException>(() => tx.CommitAsync(Ct).AsTask());
        await Assert.ThrowsAsync<InvalidOperationException>(() => tx.RollbackAsync(Ct).AsTask());

        scope.Dispose();
        scope.Dispose();
        Assert.False(tx.IsBusy);
        Assert.True(tx.CanCommit);
    }

    [Fact]
    public async Task Commit_without_a_transaction_throws()
    {
        var (tx, _, _) = Make();
        await Assert.ThrowsAsync<InvalidOperationException>(() => tx.CommitAsync(Ct).AsTask());
        await Assert.ThrowsAsync<InvalidOperationException>(() => tx.RollbackAsync(Ct).AsTask());
    }

    [Fact]
    public async Task Status_text_for_the_four_situations_in_both_languages()
    {
        var (tx, _, time) = Make();
        foreach (var (lang, manual, open, failed) in new[]
        {
            (UiLanguage.French, "Transaction manuelle", "Transaction ouverte depuis 4 min · 2 instructions", "Transaction en échec : annulez-la"),
            (UiLanguage.English, "Manual transaction", "Transaction open for 4 minutes · 2 statements", "Transaction failed: roll it back"),
        })
        {
            using var _ = UiText.Override(lang);
            var t = new SessionTransaction(new ProbedConnection(), DatabaseDialect.PostgreSql, time);
            Assert.Equal("", t.StatusText);
            await t.SetModeAsync(TransactionMode.Manual, Ct);
            Assert.Equal(manual, t.StatusText);
            await t.EnsureOpenAsync(T("UPDATE t SET a = 1"), Ct);
            t.ObserveSuccess("UPDATE a", T("UPDATE a SET b = 1"), 1, "t");
            t.ObserveSuccess("UPDATE b", T("UPDATE b SET b = 1"), 1, "t");
            time.Now += TimeSpan.FromMinutes(4);
            t.RefreshElapsed();
            Assert.Equal(open, t.StatusText);
            await t.ObserveFailureAsync(new InvalidOperationException(), T("UPDATE t SET a = 1"), Ct);
            Assert.Equal(failed, t.StatusText);
            time.Now -= TimeSpan.FromMinutes(4);
        }

        Assert.NotNull(tx);
    }

    [Theory]
    [InlineData(0, "moins d'une minute")]
    [InlineData(5, "5 min")]
    [InlineData(65, "1 h 05 min")]
    public async Task Elapsed_text_is_worded_by_size(int minutes, string expected)
    {
        var (tx, _, time) = Make();
        using var _ = UiText.Override(UiLanguage.French);
        await OpenManual(tx);
        time.Now += TimeSpan.FromMinutes(minutes);
        Assert.Equal(expected, tx.ElapsedText);
    }

    [Fact]
    public async Task Dispose_sends_one_rollback_and_swallows_its_failure()
    {
        var (tx, c, _) = Make();
        await OpenManual(tx);
        c.Fail = sql => sql == "ROLLBACK" ? new InvalidOperationException("gone") : null;

        await tx.DisposeAsync();

        Assert.Equal(1, c.Executed.Count(s => s == "ROLLBACK"));
    }

    [Fact]
    public async Task Dispose_without_a_transaction_sends_nothing()
    {
        var (tx, c, _) = Make();
        await tx.DisposeAsync();
        Assert.Empty(c.Executed);
    }

    [Fact]
    public async Task A_connection_without_a_probe_does_not_support_manual_mode()
    {
        var tx = new SessionTransaction(new Connection(), DatabaseDialect.Oracle, TimeProvider.System);
        Assert.False(tx.IsSupported);
        await Assert.ThrowsAsync<InvalidOperationException>(() => tx.SetModeAsync(TransactionMode.Manual, Ct).AsTask());
        Assert.Equal(TransactionMode.AutoCommit, tx.Mode);
    }
}
