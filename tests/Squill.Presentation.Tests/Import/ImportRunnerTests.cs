using System.Text;
using Squill.Core;
using Squill.Data;
using Squill.Presentation.Import;
using Squill.Presentation.Safety;
using Squill.Presentation.Sessions;
using Squill.Sql.Transactions;
using Xunit;

namespace Squill.Presentation.Tests.Import;

public sealed class ImportRunnerTests : IDisposable
{
    private static readonly ColumnDefinition[] Columns = [new("id", "int", false, true, null), new("name", "text", true, false, null)];
    private static readonly TableInfo Table = new(null, "items", TableKind.Table, Columns, [], []);
    private readonly string _dir = Directory.CreateTempSubdirectory("squill-import-").FullName;

    public void Dispose() => Directory.Delete(_dir, true);

    private sealed class Reader : IResultReader
    {
        public IReadOnlyList<ColumnInfo> Columns => [];
        public long RecordsAffected => 1;
        public ValueTask<object?[][]?> ReadBatchAsync(int maxRows, CancellationToken cancellationToken) => ValueTask.FromResult<object?[][]?>(null);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class Conn(ManualTime? clock = null) : IDatabaseConnection
    {
        public List<string> Sql { get; } = [];
        public Func<string, IReadOnlyList<QueryParameter>, bool>? FailWhen { get; set; }
        public Action? OnInsert { get; set; }
        public string ServerVersion => "1";
        public IEnumerable<string> Inserts => Sql.Where(s => s.StartsWith("INSERT", StringComparison.Ordinal));

        public ValueTask<IResultReader> ExecuteAsync(string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken)
        {
            Sql.Add(sql);
            if (sql.StartsWith("INSERT", StringComparison.Ordinal))
            {
                clock?.Advance(100);
                OnInsert?.Invoke();
                if (FailWhen?.Invoke(sql, parameters) == true)
                    throw new InvalidOperationException("constraint failed\nsecond line");
            }

            return ValueTask.FromResult<IResultReader>(new Reader());
        }

        public ValueTask<IReadOnlyList<string>> ListDatabasesAsync(CancellationToken cancellationToken) => ValueTask.FromResult<IReadOnlyList<string>>([]);
        public ValueTask<SchemaSnapshot> ReadSchemaAsync(CancellationToken cancellationToken) => ValueTask.FromResult(SchemaSnapshot.Empty);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class ManualTime : TimeProvider
    {
        private long _ms;
        public override long TimestampFrequency => 1000;
        public override long GetTimestamp() => _ms;
        public void Advance(long ms) => _ms += ms;
    }

    private sealed class Sink : IProgress<ImportProgress>
    {
        public List<ImportProgress> Reports { get; } = [];
        public void Report(ImportProgress value) => Reports.Add(value);
    }

    private static ConnectionSession Session(Conn c, DatabaseDialect d = DatabaseDialect.PostgreSql, ConnectionEnvironment env = ConnectionEnvironment.None) =>
        new(new ConnectionProfile(Guid.NewGuid(), "dev", d, "h", 1, "db", "u", null, null, env), c, SchemaSnapshot.Empty, null);

    private string Csv(params string[] lines)
    {
        var path = Path.Combine(_dir, "items.csv");
        File.WriteAllLines(path, ["id,name", .. lines], new UTF8Encoding(false));
        return path;
    }

    private static string[] Rows(int count) => [.. Enumerable.Range(1, count).Select(i => $"{i},n{i}")];

    private static ImportJob Job(ConnectionSession s, string path, ImportOptions? options = null, TableInfo? table = null) =>
        new(s, table ?? Table, ImportMapper.AutoMap(["id", "name"], (table ?? Table).Columns), ImportFormat.Default, CsvOptions.Default, path, options ?? new ImportOptions());

    private static ImportRunner Runner(FakeDialogService? dialogs = null, TimeProvider? time = null) => new(dialogs ?? new FakeDialogService(), time);

    [Theory]
    [InlineData(DatabaseDialect.SqlServer, 5, 1000, 420)]
    [InlineData(DatabaseDialect.SqlServer, 1, 5000, 1000)]
    [InlineData(DatabaseDialect.Sqlite, 10, 5000, 3276)]
    [InlineData(DatabaseDialect.PostgreSql, 10, 1000, 1000)]
    [InlineData(DatabaseDialect.MySql, 100000, 1000, 1)]
    [InlineData(DatabaseDialect.Oracle, 3, 1000, 1)]
    public void ClampBatchSize_respects_each_engine_limit(DatabaseDialect d, int columns, int requested, int expected) =>
        Assert.Equal(expected, ImportRunner.ClampBatchSize(d, columns, requested));

    [Fact]
    public async Task Run_sends_one_multi_row_insert_per_batch_in_its_own_transaction()
    {
        var c = new Conn();
        var result = await Runner().RunAsync(Job(Session(c), Csv(Rows(2500)), new ImportOptions { BatchSize = 1000 }), new Sink(), CancellationToken.None);
        Assert.Equal(ImportOutcome.Completed, result.Completed);
        Assert.Equal((2500, 2500, 0), (result.Read, result.Inserted, result.Rejected));
        Assert.Equal(3, c.Inserts.Count());
        Assert.Equal(3, c.Sql.Count(s => s == "BEGIN"));
        Assert.Equal(3, c.Sql.Count(s => s == "COMMIT"));
        Assert.Contains("VALUES (@p0, @p1), (@p2, @p3)", c.Inserts.First());
        Assert.Null(result.RejectsPath);
    }

    [Fact]
    public async Task Run_clamps_the_batch_to_the_parameter_limit()
    {
        var c = new Conn();
        await Runner().RunAsync(Job(Session(c, DatabaseDialect.SqlServer), Csv(Rows(1500)), new ImportOptions { BatchSize = 5000 }), new Sink(), CancellationToken.None);
        Assert.Equal(2, c.Inserts.Count()); // 1,000 VALUES rows at most on SQL Server
    }

    [Fact]
    public async Task Reject_finds_the_bad_rows_by_halving_and_writes_the_rejects_file()
    {
        var c = new Conn { FailWhen = (_, p) => p.Any(x => x.Value as string == "n7") };
        var lines = Rows(20);
        lines[2] = "x,bad number";
        var result = await Runner().RunAsync(Job(Session(c), Csv(lines)), new Sink(), CancellationToken.None);
        Assert.Equal(ImportOutcome.Completed, result.Completed);
        Assert.Equal((20, 18, 2), (result.Read, result.Inserted, result.Rejected));
        Assert.Equal(Path.Combine(_dir, "items.rejets.csv"), result.RejectsPath);
        var rejected = File.ReadAllLines(result.RejectsPath!);
        Assert.Equal("id,name,erreur", rejected[0]);
        Assert.Equal(3, rejected.Length);
        Assert.StartsWith("x,bad number,", rejected[1]);
        Assert.Equal("7,n7,constraint failed", rejected[2]);
    }

    [Fact]
    public async Task Reject_failing_every_row_of_a_batch_is_a_failure_not_a_row_problem()
    {
        var c = new Conn { FailWhen = (_, _) => true };
        var result = await Runner().RunAsync(Job(Session(c), Csv(Rows(10))), new Sink(), CancellationToken.None);
        Assert.Equal(ImportOutcome.Failed, result.Completed);
        Assert.Equal("constraint failed", result.Error);
        Assert.Equal(0, result.Inserted);
    }

    [Fact]
    public async Task Stop_ends_at_the_first_bad_row_and_keeps_what_was_committed()
    {
        var c = new Conn { FailWhen = (_, p) => p.Any(x => x.Value as string == "n6") };
        var result = await Runner().RunAsync(Job(Session(c), Csv(Rows(20)), new ImportOptions { OnError = ImportErrorPolicy.Stop, BatchSize = 10 }), new Sink(), CancellationToken.None);
        Assert.Equal(ImportOutcome.StoppedOnError, result.Completed);
        Assert.Equal(5, result.Inserted);
        Assert.Equal(1, result.Rejected);
        Assert.Equal("constraint failed", result.Error);
    }

    [Fact]
    public async Task Stop_on_a_conversion_error_first_writes_the_rows_read_before_it()
    {
        var c = new Conn();
        var lines = Rows(8);
        lines[5] = "x,bad";
        var result = await Runner().RunAsync(Job(Session(c), Csv(lines), new ImportOptions { OnError = ImportErrorPolicy.Stop }), new Sink(), CancellationToken.None);
        Assert.Equal((ImportOutcome.StoppedOnError, 5L, 1L), (result.Completed, result.Inserted, result.Rejected));
    }

    [Fact]
    public async Task SingleTransaction_rolls_everything_back_on_a_bad_row()
    {
        var c = new Conn();
        var lines = Rows(30);
        lines[25] = "x,bad";
        var result = await Runner().RunAsync(Job(Session(c), Csv(lines), new ImportOptions { CommitMode = ImportCommitMode.SingleTransaction, BatchSize = 10 }), new Sink(), CancellationToken.None);
        Assert.Equal(ImportOutcome.StoppedOnError, result.Completed);
        Assert.Equal(0, result.Inserted);
        Assert.Equal("BEGIN", c.Sql[0]);
        Assert.Equal("ROLLBACK", c.Sql[^1]);
        Assert.DoesNotContain("COMMIT", c.Sql);
    }

    [Fact]
    public async Task SingleTransaction_commits_once_when_all_rows_are_good()
    {
        var c = new Conn();
        var result = await Runner().RunAsync(Job(Session(c), Csv(Rows(30)), new ImportOptions { CommitMode = ImportCommitMode.SingleTransaction, BatchSize = 10 }), new Sink(), CancellationToken.None);
        Assert.Equal((ImportOutcome.Completed, 30L), (result.Completed, result.Inserted));
        Assert.Equal(1, c.Sql.Count(s => s == "COMMIT"));
    }

    [Fact]
    public async Task Cancel_finishes_the_current_batch_and_reports_cancelled()
    {
        using var cts = new CancellationTokenSource();
        var c = new Conn { OnInsert = cts.Cancel };
        var result = await Runner().RunAsync(Job(Session(c), Csv(Rows(50)), new ImportOptions { BatchSize = 10 }), new Sink(), cts.Token);
        Assert.Equal(ImportOutcome.Cancelled, result.Completed);
        Assert.Equal(10, result.Inserted);
        Assert.Single(c.Inserts);
    }

    [Fact]
    public async Task Progress_is_throttled_to_one_report_every_250_ms_plus_the_final_one()
    {
        var clock = new ManualTime();
        var c = new Conn(clock);
        var sink = new Sink();
        await Runner(time: clock).RunAsync(Job(Session(c), Csv(Rows(100)), new ImportOptions { BatchSize = 10 }), sink, CancellationToken.None);
        Assert.InRange(sink.Reports.Count, 2, 5);
        Assert.Equal(100, sink.Reports[^1].Inserted);
        Assert.True(sink.Reports[^1].RowsPerSecond > 0);
    }

    [Fact]
    public async Task Inside_a_user_transaction_each_batch_uses_a_savepoint_and_never_commits()
    {
        var c = new Conn();
        var session = Session(c);
        session.Transaction.ObserveSuccess("BEGIN", StatementClassifier.Classify("BEGIN", DatabaseDialect.PostgreSql), -1, "x");
        c.Sql.Clear();
        var result = await Runner().RunAsync(Job(session, Csv(Rows(20)), new ImportOptions { BatchSize = 10 }), new Sink(), CancellationToken.None);
        Assert.Equal(20, result.Inserted);
        Assert.Equal(2, c.Sql.Count(s => s.StartsWith("SAVEPOINT", StringComparison.Ordinal)));
        Assert.Equal(2, c.Sql.Count(s => s.StartsWith("RELEASE SAVEPOINT", StringComparison.Ordinal)));
        Assert.DoesNotContain("BEGIN", c.Sql);
        Assert.DoesNotContain("COMMIT", c.Sql);
        Assert.Equal(2, session.Transaction.PendingCount);
    }

    [Fact]
    public async Task Production_declined_runs_nothing_and_asks_for_the_table_name()
    {
        var c = new Conn();
        var dialogs = new FakeDialogService { ProductionResult = new ProductionConfirmationResult(false, false) };
        var result = await Runner(dialogs).RunAsync(Job(Session(c, env: ConnectionEnvironment.Production), Csv(Rows(5))), new Sink(), CancellationToken.None);
        Assert.Equal(ImportOutcome.Cancelled, result.Completed);
        Assert.Empty(c.Sql);
        Assert.Equal("items", Assert.Single(dialogs.ProductionRequests).RequiredText);
    }

    [Fact]
    public async Task Production_confirmed_runs_once_the_confirmation_was_asked_a_single_time()
    {
        var c = new Conn();
        var dialogs = new FakeDialogService();
        var result = await Runner(dialogs).RunAsync(Job(Session(c, env: ConnectionEnvironment.Production), Csv(Rows(2500))), new Sink(), CancellationToken.None);
        Assert.Equal(2500, result.Inserted);
        Assert.Single(dialogs.ProductionRequests);
    }

    [Fact]
    public async Task An_identity_column_on_sql_server_is_refused_before_any_statement()
    {
        var c = new Conn();
        var table = new TableInfo(null, "items", TableKind.Table, [new("id", "int", false, true, null, IsAutoIncrement: true), Columns[1]], [], []);
        var result = await Runner().RunAsync(Job(Session(c, DatabaseDialect.SqlServer), Csv(Rows(3)), table: table), new Sink(), CancellationToken.None);
        Assert.Equal(ImportOutcome.Failed, result.Completed);
        Assert.Contains("id", result.Error);
        Assert.Empty(c.Sql);
    }

    [Fact]
    public async Task A_rejects_line_quotes_fields_that_hold_the_delimiter()
    {
        var c = new Conn();
        var result = await Runner().RunAsync(Job(Session(c), Csv("x,\"a,b\"")), new Sink(), CancellationToken.None);
        Assert.Equal(1, result.Rejected);
        Assert.StartsWith("x,\"a,b\",", File.ReadAllLines(result.RejectsPath!)[1]);
    }
}
