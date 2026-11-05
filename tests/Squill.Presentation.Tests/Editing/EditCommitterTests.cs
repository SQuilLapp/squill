using Squill.Core;
using Squill.Core.Editing;
using Squill.Data;
using Squill.Presentation.Editing;
using Squill.Presentation.Localization;
using Xunit;

namespace Squill.Presentation.Tests.Editing;

public sealed class EditCommitterTests
{
    private static readonly ColumnDefinition[] Defs =
    [
        new("Id", "int", false, true, null, true),
        new("Name", "text", false, false, null),
    ];

    private static readonly EditTarget Target =
        new(null, "T", [.. Defs.Select((d, i) => new EditColumn(i, d))], ["Id"], new TableInfo(null, "T", TableKind.Table, Defs, [], []));

    private static readonly ColumnInfo[] Cols = [new("Id", "int", typeof(int), false, 0), new("Name", "text", typeof(string), false, 1)];

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
        public List<IReadOnlyList<QueryParameter>> Params { get; } = [];
        public Queue<long> Affected { get; } = new();
        public Queue<object?[][]> Rows { get; } = new();
        public Func<string, Exception?>? Fail { get; set; }
        public string ServerVersion => "1";

        public ValueTask<IResultReader> ExecuteAsync(string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken)
        {
            Sql.Add(sql);
            Params.Add(parameters);
            if (Fail?.Invoke(sql) is { } ex)
                throw ex;
            var dml = sql.StartsWith("UPDATE", StringComparison.Ordinal) || sql.StartsWith("DELETE", StringComparison.Ordinal);
            var isKey = sql.Contains("RETURNING", StringComparison.Ordinal) || sql.Contains("SCOPE_IDENTITY", StringComparison.Ordinal) || sql.Contains("LAST_INSERT_ID", StringComparison.Ordinal);
            return ValueTask.FromResult<IResultReader>(new Reader(isKey && Rows.Count > 0 ? Rows.Dequeue() : [], dml && Affected.Count > 0 ? Affected.Dequeue() : -1));
        }

        public ValueTask<IReadOnlyList<string>> ListDatabasesAsync(CancellationToken cancellationToken) => ValueTask.FromResult<IReadOnlyList<string>>([]);

        public ValueTask<SchemaSnapshot> ReadSchemaAsync(CancellationToken cancellationToken) => ValueTask.FromResult(SchemaSnapshot.Empty);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static PendingEdit Del(long row, int id) => new(row, new DeleteRow([new ColumnValue("Id", id)]));
    private static PendingEdit Upd(long row, int id) => new(row, new UpdateRow([new ColumnValue("Id", id)], [new ColumnValue("Name", "n")]));
    private static PendingEdit Ins(long row) => new(row, new InsertRow([new ColumnValue("Name", "n")]));

    [Fact]
    public async Task EmptyListExecutesNothing()
    {
        var c = new Conn();
        var r = await new EditCommitter(c, DatabaseDialect.Sqlite).CommitAsync(Target, Cols, [], CancellationToken.None);
        Assert.Empty(c.Sql);
        Assert.Equal(0, r.StatementCount);
    }

    [Theory]
    [InlineData(DatabaseDialect.Sqlite, "BEGIN", "COMMIT")]
    [InlineData(DatabaseDialect.SqlServer, "BEGIN TRANSACTION", "COMMIT TRANSACTION")]
    public async Task RunsInOrderInsideBracketWithParameters(DatabaseDialect d, string begin, string commit)
    {
        var c = new Conn();
        c.Affected.Enqueue(1);
        c.Affected.Enqueue(1);
        c.Rows.Enqueue([[7L]]);
        var r = await new EditCommitter(c, d).CommitAsync(Target, Cols, [Del(0, 1), Upd(1, 2), Ins(9)], CancellationToken.None);
        Assert.Equal(5, c.Sql.Count);
        Assert.Equal(begin, c.Sql[0]);
        Assert.StartsWith("DELETE", c.Sql[1]);
        Assert.StartsWith("UPDATE", c.Sql[2]);
        Assert.StartsWith("INSERT", c.Sql[3]);
        Assert.Equal(commit, c.Sql[4]);
        Assert.Equal(new QueryParameter("p0", 1), Assert.Single(c.Params[1]));
        Assert.Equal(["p0", "p1"], c.Params[2].Select(p => p.Name));
        Assert.Equal(3, r.StatementCount);
        Assert.Equal(7, r.GeneratedKeys[9]);
    }

    [Theory]
    [InlineData(0L, "was not found")]
    [InlineData(2L, "matched more than one row")]
    public async Task WrongAffectedCountRollsBackAndCarriesRow(long affected, string text)
    {
        using var _ = UiText.Override(UiLanguage.English);
        var c = new Conn();
        c.Affected.Enqueue(affected);
        var ex = await Assert.ThrowsAsync<EditCommitException>(() => new EditCommitter(c, DatabaseDialect.Sqlite).CommitAsync(Target, Cols, [Upd(4, 1)], CancellationToken.None));
        Assert.Equal(4, ex.Row);
        Assert.Contains(text, ex.Message);
        Assert.Equal("ROLLBACK", c.Sql[^1]);
    }

    [Fact]
    public async Task UnknownAffectedCountIsAccepted()
    {
        var c = new Conn();
        await new EditCommitter(c, DatabaseDialect.Sqlite).CommitAsync(Target, Cols, [Del(0, 1)], CancellationToken.None);
        Assert.Equal("COMMIT", c.Sql[^1]);
    }

    [Fact]
    public async Task ProviderExceptionRollsBackAndCarriesRow()
    {
        var c = new Conn { Fail = s => s.StartsWith("UPDATE", StringComparison.Ordinal) ? new InvalidOperationException("boom") : null };
        c.Affected.Enqueue(1);
        var ex = await Assert.ThrowsAsync<EditCommitException>(() => new EditCommitter(c, DatabaseDialect.Sqlite).CommitAsync(Target, Cols, [Del(0, 1), Upd(3, 2)], CancellationToken.None));
        Assert.Equal(3, ex.Row);
        Assert.Equal("boom", ex.Message);
        Assert.Equal("ROLLBACK", c.Sql[^1]);
    }

    [Fact]
    public async Task CommitFailureHasNoRow()
    {
        var c = new Conn { Fail = s => s == "COMMIT" ? new InvalidOperationException("late") : null };
        var ex = await Assert.ThrowsAsync<EditCommitException>(() => new EditCommitter(c, DatabaseDialect.Sqlite).CommitAsync(Target, Cols, [Del(0, 1)], CancellationToken.None));
        Assert.Null(ex.Row);
    }

    [Fact]
    public async Task CancellationRollsBackAndPropagates()
    {
        var c = new Conn { Fail = s => s.StartsWith("DELETE", StringComparison.Ordinal) ? new OperationCanceledException() : null };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new EditCommitter(c, DatabaseDialect.Sqlite).CommitAsync(Target, Cols, [Del(0, 1)], CancellationToken.None));
        Assert.Equal("ROLLBACK", c.Sql[^1]);
    }

    [Theory]
    [InlineData(DatabaseDialect.PostgreSql)]
    [InlineData(DatabaseDialect.Sqlite)]
    [InlineData(DatabaseDialect.SqlServer)]
    public async Task GeneratedKeyComesFromInsertResult(DatabaseDialect d)
    {
        var c = new Conn();
        c.Rows.Enqueue([[5L]]);
        var r = await new EditCommitter(c, d).CommitAsync(Target, Cols, [Ins(2)], CancellationToken.None);
        Assert.Equal(3, c.Sql.Count);
        Assert.Equal(5, Assert.IsType<int>(r.GeneratedKeys[2]));
    }

    [Fact]
    public async Task MySqlKeyComesFromLastInsertIdAndIsConvertedFromUlongToInt()
    {
        var c = new Conn();
        c.Rows.Enqueue([[(object?)12UL]]);
        var r = await new EditCommitter(c, DatabaseDialect.MySql).CommitAsync(Target, Cols, [Ins(1)], CancellationToken.None);
        Assert.Equal(4, c.Sql.Count);
        Assert.Equal("SELECT LAST_INSERT_ID()", c.Sql[2]);
        Assert.Equal(12, Assert.IsType<int>(r.GeneratedKeys[1]));
    }

    [Fact]
    public async Task KeyIsConvertedToResultColumnType()
    {
        var c = new Conn();
        c.Rows.Enqueue([[5m]]);
        ColumnInfo[] cols = [new("Id", "bigint", typeof(long), false, 0), Cols[1]];
        var r = await new EditCommitter(c, DatabaseDialect.SqlServer).CommitAsync(Target, cols, [Ins(0)], CancellationToken.None);
        Assert.Equal(5L, Assert.IsType<long>(r.GeneratedKeys[0]));
    }

    [Fact]
    public async Task SqliteRoundTripCommitsAndRollsBack()
    {
        var path = Path.Combine(Path.GetTempPath(), $"squill-{Guid.NewGuid():N}.db");
        var profile = new ConnectionProfile(Guid.NewGuid(), "f", DatabaseDialect.Sqlite, null, null, null, null, path, null);
        try
        {
            await using var conn = await new Squill.Data.Sqlite.SqliteDatabaseProvider().OpenAsync(profile, null, CancellationToken.None);
            async Task<List<object?[]>> Q(string sql)
            {
                await using var reader = await conn.ExecuteAsync(sql, [], CancellationToken.None);
                return await reader.ReadAllAsync(100, CancellationToken.None);
            }

            await Q("CREATE TABLE T (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL)");
            await Q("INSERT INTO T (Name) VALUES ('a'), ('b'), ('c')");
            var committer = new EditCommitter(conn, DatabaseDialect.Sqlite);

            var r = await committer.CommitAsync(Target, Cols, [Del(0, 1), Upd(1, 2), Ins(5)], CancellationToken.None);
            Assert.Equal(4, Assert.IsType<int>(r.GeneratedKeys[5]));
            Assert.Equal(["2:n", "3:c", "4:n"], (await Q("SELECT Id || ':' || Name FROM T ORDER BY Id")).Select(x => (string)x[0]!));

            var ex = await Assert.ThrowsAsync<EditCommitException>(() => committer.CommitAsync(Target, Cols, [Ins(7), Upd(8, 99)], CancellationToken.None));
            Assert.Equal(8, ex.Row);
            Assert.Equal(3L, (await Q("SELECT COUNT(*) FROM T"))[0][0]);
        }
        finally
        {
            SqliteCleanup(path);
        }
    }

    private static void SqliteCleanup(string path)
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(path);
    }

    [Theory]
    [InlineData(UiLanguage.French, 0L, "La ligne 4 est introuvable : elle a été modifiée ou supprimée depuis son chargement")]
    [InlineData(UiLanguage.French, 2L, "La ligne 4 correspond à plusieurs lignes")]
    [InlineData(UiLanguage.English, 0L, "Row 4 was not found: it was changed or deleted since it was loaded")]
    [InlineData(UiLanguage.English, 2L, "Row 4 matched more than one row")]
    public async Task WrongAffectedCountMessageIsLocalized(UiLanguage language, long affected, string expected)
    {
        using var _ = UiText.Override(language);
        var c = new Conn();
        c.Affected.Enqueue(affected);

        var ex = await Assert.ThrowsAsync<EditCommitException>(() => new EditCommitter(c, DatabaseDialect.Sqlite).CommitAsync(Target, Cols, [Upd(4, 1)], CancellationToken.None));

        Assert.Equal(expected, ex.Message);
    }
}
