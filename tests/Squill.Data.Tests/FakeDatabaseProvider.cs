using Squill.Core;

namespace Squill.Data.Tests;

/// <summary>Scripted provider: every statement returns the same columns and rows.</summary>
internal sealed class FakeDatabaseProvider(DatabaseDialect dialect, IReadOnlyList<ColumnInfo> columns, object?[][] rows) : IDatabaseProvider
{
    public DatabaseDialect Dialect { get; } = dialect;

    public ValueTask<IDatabaseConnection> OpenAsync(ConnectionProfile profile, string? password, CancellationToken cancellationToken) =>
        ValueTask.FromResult<IDatabaseConnection>(new FakeConnection(columns, rows));

    private sealed class FakeConnection(IReadOnlyList<ColumnInfo> columns, object?[][] rows) : IDatabaseConnection
    {
        public string ServerVersion => "fake-1.0";

        public ValueTask<IResultReader> ExecuteAsync(string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IResultReader>(new FakeResultReader(columns, rows));

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
