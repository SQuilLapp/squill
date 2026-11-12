using Squill.Core;
using Squill.Core.Plans;
using Squill.Core.Transactions;
using Squill.Data;
using Squill.Presentation.Safety;
using Squill.Presentation.Sessions;
using Squill.Sql.Transactions;
using Xunit;

namespace Squill.Presentation.Tests.Safety;

public sealed class ImpactEstimatorTests
{
    private sealed class Conn : IDatabaseConnection, IPlanReader
    {
        public PlanNode Root { get; set; } = Node("Seq Scan", 1000);
        public Exception? Failure { get; set; }
        public Func<CancellationToken, Task>? Gate { get; set; }
        public int Calls { get; private set; }
        public string ServerVersion => "1";
        public bool SupportsActualPlan => false;

        public async ValueTask<QueryPlan> ExplainAsync(string statement, IReadOnlyList<QueryParameter> parameters, PlanMode mode, CancellationToken cancellationToken)
        {
            Calls++;
            if (Gate is { } gate)
                await gate(cancellationToken);
            return Failure is { } ex
                ? throw ex
                : new QueryPlan(DatabaseDialect.PostgreSql, mode, statement, Root, [], [], null, null, null, false, PlanSourceFormat.Json, "{}");
        }

        public ValueTask<IResultReader> ExecuteAsync(string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<IReadOnlyList<string>> ListDatabasesAsync(CancellationToken cancellationToken) => ValueTask.FromResult<IReadOnlyList<string>>([]);
        public ValueTask<SchemaSnapshot> ReadSchemaAsync(CancellationToken cancellationToken) => ValueTask.FromResult(SchemaSnapshot.Empty);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static PlanNode Node(string operation, double? rows, params PlanNode[] children) =>
        new(0, operation, PlanNodeKind.Other, null, null, rows, null, null, null, null, null, null, [], [], false, children);

    private static (ConnectionSession Session, Conn C) Make(DatabaseDialect d = DatabaseDialect.PostgreSql)
    {
        var c = new Conn();
        return (new ConnectionSession(new ConnectionProfile(Guid.NewGuid(), "p", d, "h", 1, "d", "u", null, null), c, SchemaSnapshot.Empty, null), c);
    }

    [Theory]
    [InlineData("DELETE FROM t")]
    [InlineData("UPDATE t SET a = 1 WHERE b > 3")]
    [InlineData("INSERT INTO t SELECT * FROM u")]
    public async Task Estimates_update_delete_and_insert_select(string sql)
    {
        var (s, _) = Make();
        Assert.Equal(new ImpactEstimate(1000), await ImpactEstimator.EstimateAsync(s, sql, [], CancellationToken.None));
    }

    [Theory]
    [InlineData("INSERT INTO t VALUES (1)")]
    [InlineData("SELECT 1")]
    [InlineData("CREATE TABLE x (a int)")]
    [InlineData("")]
    public async Task Skips_other_statements_without_calling_the_engine(string sql)
    {
        var (s, c) = Make();
        Assert.Null(await ImpactEstimator.EstimateAsync(s, sql, [], CancellationToken.None));
        Assert.Equal(0, c.Calls);
    }

    [Fact]
    public async Task Sqlite_returns_null_without_a_call()
    {
        var (s, c) = Make(DatabaseDialect.Sqlite);
        Assert.Null(await ImpactEstimator.EstimateAsync(s, "DELETE FROM t", [], CancellationToken.None));
        Assert.Equal(0, c.Calls);
    }

    [Fact]
    public async Task A_failed_transaction_returns_null_without_a_call()
    {
        var (s, c) = Make();
        s.Transaction.ObserveSuccess("BEGIN", StatementClassifier.Classify("BEGIN", DatabaseDialect.PostgreSql), -1, "t");
        await s.Transaction.ObserveFailureAsync(new InvalidOperationException(), StatementClassifier.Classify("DELETE FROM t", DatabaseDialect.PostgreSql), CancellationToken.None);
        Assert.Equal(TransactionState.Failed, s.Transaction.State);

        Assert.Null(await ImpactEstimator.EstimateAsync(s, "DELETE FROM t", [], CancellationToken.None));
        Assert.Equal(0, c.Calls);
    }

    [Theory]
    [InlineData("ModifyTable")]
    [InlineData("Delete")]
    [InlineData("Clustered Index Delete")]
    [InlineData("Table Update")]
    [InlineData("Index Insert")]
    public async Task The_wrapper_root_is_skipped_for_its_first_child_with_rows(string wrapper)
    {
        var (s, c) = Make();
        c.Root = Node(wrapper, 0, Node("Result", null, Node("Seq Scan", 1000)));
        Assert.Equal(1000, (await ImpactEstimator.EstimateAsync(s, "DELETE FROM t", [], CancellationToken.None))!.Rows);
    }

    [Fact]
    public async Task Rounds_and_never_goes_below_zero()
    {
        var (s, c) = Make();
        c.Root = Node("Seq Scan", 1247.6);
        Assert.Equal(1248, (await ImpactEstimator.EstimateAsync(s, "DELETE FROM t", [], CancellationToken.None))!.Rows);
        c.Root = Node("Seq Scan", -3);
        Assert.Equal(0, (await ImpactEstimator.EstimateAsync(s, "DELETE FROM t", [], CancellationToken.None))!.Rows);
    }

    [Fact]
    public async Task Nothing_found_returns_null()
    {
        var (s, c) = Make();
        c.Root = Node("ModifyTable", 0, Node("Result", null));
        Assert.Null(await ImpactEstimator.EstimateAsync(s, "DELETE FROM t", [], CancellationToken.None));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task An_engine_failure_returns_null_and_never_throws(int kind)
    {
        var (s, c) = Make();
        c.Failure = kind == 0 ? new PlanUnavailableException("x", "no") : new InvalidOperationException("boom");
        Assert.Null(await ImpactEstimator.EstimateAsync(s, "DELETE FROM t", [], CancellationToken.None));
    }

    [Fact]
    public async Task A_slow_engine_returns_null_after_the_cap()
    {
        var (s, c) = Make();
        c.Gate = ct => Task.Delay(TimeSpan.FromSeconds(30), ct);
        Assert.Null(await ImpactEstimator.EstimateAsync(s, "DELETE FROM t", [], TimeSpan.FromMilliseconds(50), CancellationToken.None));
    }

    [Fact]
    public async Task The_callers_cancellation_propagates()
    {
        var (s, c) = Make();
        using var cts = new CancellationTokenSource();
        c.Gate = async ct =>
        {
            await cts.CancelAsync();
            await Task.Delay(TimeSpan.FromSeconds(30), ct);
        };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ImpactEstimator.EstimateAsync(s, "DELETE FROM t", [], cts.Token).AsTask());
    }
}
