using Squill.Core;
using Squill.Core.Plans;

namespace Squill.Data.Tests;

/// <summary>Scripted provider: every statement returns the same columns and rows.</summary>
internal sealed class FakeDatabaseProvider(DatabaseDialect dialect, IReadOnlyList<ColumnInfo> columns, object?[][] rows) : IDatabaseProvider
{
    public DatabaseDialect Dialect { get; } = dialect;

    /// <summary>Gets or sets the plan returned by <see cref="IPlanReader.ExplainAsync"/>.</summary>
    public QueryPlan? Plan { get; set; }

    /// <summary>Gets or sets an exception thrown by <see cref="IPlanReader.ExplainAsync"/> instead of returning <see cref="Plan"/>.</summary>
    public Exception? PlanException { get; set; }

    /// <summary>Gets the recorded plan requests, in order.</summary>
    public List<(string Sql, PlanMode Mode)> PlanCalls { get; } = [];

    public ValueTask<IDatabaseConnection> OpenAsync(ConnectionProfile profile, string? password, CancellationToken cancellationToken) =>
        ValueTask.FromResult<IDatabaseConnection>(new FakeConnection(this, columns, rows));

    private sealed class FakeConnection(FakeDatabaseProvider owner, IReadOnlyList<ColumnInfo> columns, object?[][] rows) : IDatabaseConnection, IPlanReader
    {
        public string ServerVersion => "fake-1.0";

        public bool SupportsActualPlan => true;

        public ValueTask<QueryPlan> ExplainAsync(string sql, IReadOnlyList<QueryParameter> parameters, PlanMode mode, CancellationToken cancellationToken)
        {
            owner.PlanCalls.Add((sql, mode));
            if (owner.PlanException is { } ex)
                throw ex;
            return ValueTask.FromResult(owner.Plan ?? throw new InvalidOperationException("No plan configured."));
        }

        public ValueTask<IResultReader> ExecuteAsync(string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IResultReader>(new FakeResultReader(columns, rows));

        public ValueTask<IReadOnlyList<string>> ListDatabasesAsync(CancellationToken cancellationToken) => ValueTask.FromResult<IReadOnlyList<string>>([]);

        public ValueTask<SchemaSnapshot> ReadSchemaAsync(CancellationToken cancellationToken) => ValueTask.FromResult(SchemaSnapshot.Empty);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    internal sealed class FakeResultReader(IReadOnlyList<ColumnInfo> columns, object?[][] rows) : IResultReader
    {
        private int _next;

        public IReadOnlyList<ColumnInfo> Columns => columns;

        public long RecordsAffected => _next >= rows.Length ? rows.Length : -1;

        public ValueTask<object?[][]?> ReadBatchAsync(int maxRows, CancellationToken cancellationToken)
        {
            if (_next >= rows.Length)
                return ValueTask.FromResult<object?[][]?>(null);
            var batch = rows.Skip(_next).Take(maxRows).ToArray();
            _next += batch.Length;
            return ValueTask.FromResult<object?[][]?>(batch);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
