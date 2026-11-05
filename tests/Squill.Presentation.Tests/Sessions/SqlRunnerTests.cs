using Squill.Core;
using Squill.Data;
using Squill.Presentation.Sessions;
using Xunit;

namespace Squill.Presentation.Tests.Sessions;

public sealed class SqlRunnerTests
{
    private sealed class FakeReader(object?[][] rows, long affected) : IResultReader
    {
        private int _next;
        public bool Disposed { get; private set; }
        public int Drained { get; private set; }
        public IReadOnlyList<ColumnInfo> Columns => [];
        public long RecordsAffected => affected;

        public ValueTask<object?[][]?> ReadBatchAsync(int maxRows, CancellationToken cancellationToken)
        {
            if (_next >= rows.Length)
                return ValueTask.FromResult<object?[][]?>(null);
            var batch = rows.Skip(_next).Take(maxRows).ToArray();
            _next += batch.Length;
            Drained += batch.Length;
            return ValueTask.FromResult<object?[][]?>(batch);
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeConnection : IDatabaseConnection
    {
        public List<string> Executed { get; } = [];
        public List<IReadOnlyList<QueryParameter>> Parameters { get; } = [];
        public FakeReader? Last { get; private set; }
        public object?[][] Rows { get; set; } = [];
        public long Affected { get; set; } = -1;
        public Exception? Fail { get; set; }
        public string ServerVersion => "1";

        public ValueTask<IResultReader> ExecuteAsync(string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken)
        {
            Executed.Add(sql);
            Parameters.Add(parameters);
            if (Fail is not null)
                throw Fail;
            return ValueTask.FromResult<IResultReader>(Last = new FakeReader(Rows, Affected));
        }

        public ValueTask<IReadOnlyList<string>> ListDatabasesAsync(CancellationToken cancellationToken) => ValueTask.FromResult<IReadOnlyList<string>>([]);

        public ValueTask<SchemaSnapshot> ReadSchemaAsync(CancellationToken cancellationToken) => ValueTask.FromResult(SchemaSnapshot.Empty);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [Fact]
    public async Task ExecuteAsync_forwards_text_and_parameters_drains_and_returns_affected()
    {
        var c = new FakeConnection { Rows = [[1], [2]], Affected = 7 };
        QueryParameter[] ps = [new("p0", 5)];

        var affected = await SqlRunner.ExecuteAsync(c, "UPDATE t SET a = @p0", ps, CancellationToken.None);

        Assert.Equal(7, affected);
        Assert.Equal(["UPDATE t SET a = @p0"], c.Executed);
        Assert.Same(ps, c.Parameters[0]);
        Assert.Equal(2, c.Last!.Drained);
        Assert.True(c.Last.Disposed);
    }

    [Fact]
    public async Task ExecuteAsync_returns_minus_one_when_unknown()
    {
        var c = new FakeConnection();
        Assert.Equal(-1, await SqlRunner.ExecuteAsync(c, "x", [], CancellationToken.None));
    }

    [Fact]
    public async Task QueryFirstRowAsync_returns_first_row_and_drains()
    {
        var c = new FakeConnection { Rows = [[1, "a"], [2, "b"]] };

        var row = await SqlRunner.QueryFirstRowAsync(c, "SELECT", [], CancellationToken.None);

        Assert.Equal([1, "a"], row);
        Assert.Equal(2, c.Last!.Drained);
        Assert.True(c.Last.Disposed);
    }

    [Fact]
    public async Task QueryFirstRowAsync_returns_null_without_rows()
    {
        var c = new FakeConnection();
        Assert.Null(await SqlRunner.QueryFirstRowAsync(c, "SELECT", [], CancellationToken.None));
        Assert.True(c.Last!.Disposed);
    }

    [Theory]
    [InlineData(DatabaseDialect.SqlServer, "BEGIN TRANSACTION", "COMMIT TRANSACTION", "ROLLBACK TRANSACTION")]
    [InlineData(DatabaseDialect.PostgreSql, "BEGIN", "COMMIT", "ROLLBACK")]
    [InlineData(DatabaseDialect.Sqlite, "BEGIN", "COMMIT", "ROLLBACK")]
    public async Task Transaction_statements_follow_the_dialect(DatabaseDialect dialect, string begin, string commit, string rollback)
    {
        var c = new FakeConnection();

        await SqlRunner.BeginAsync(c, dialect, CancellationToken.None);
        await SqlRunner.CommitAsync(c, dialect, CancellationToken.None);
        await SqlRunner.RollbackAsync(c, dialect, CancellationToken.None);

        Assert.Equal([begin, commit, rollback], c.Executed);
    }

    [Fact]
    public async Task RollbackAsync_swallows_errors()
    {
        var c = new FakeConnection { Fail = new InvalidOperationException("boom") };
        await SqlRunner.RollbackAsync(c, DatabaseDialect.Sqlite, CancellationToken.None);
        Assert.Equal(["ROLLBACK"], c.Executed);
    }

    [Fact]
    public async Task RollbackAsync_does_not_swallow_cancellation()
    {
        var c = new FakeConnection { Fail = new OperationCanceledException() };
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await SqlRunner.RollbackAsync(c, DatabaseDialect.Sqlite, CancellationToken.None));
    }
}
