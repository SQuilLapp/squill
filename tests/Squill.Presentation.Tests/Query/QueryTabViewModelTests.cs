using Squill.Core;
using Squill.Data;
using Squill.Platform.Settings;
using Squill.Platform.Storage;
using Squill.Presentation.Editor;
using Squill.Presentation.Localization;
using Squill.Presentation.Query;
using Squill.Presentation.Sessions;
using Squill.Sql.Formatting;
using Xunit;

namespace Squill.Presentation.Tests.Query;

public sealed class QueryTabViewModelTests
{
    [Fact]
    public async Task FormatDocument_Script_ReplacesTextWithFormatterOutput()
    {
        var f = new Fixture("select 1 from t where a=1");
        f.Editor.CaretOffset = 0;

        await f.Vm.FormatDocumentCommand.ExecuteAsync(null);

        var expected = SqlFormatter.Format("select 1 from t where a=1", DatabaseDialect.PostgreSql, FormatOptions.Default);
        Assert.Equal(expected, f.Editor.Text);
        Assert.NotEqual("select 1 from t where a=1", f.Editor.Text);
        Assert.Equal(0, f.Editor.CaretOffset);
    }

    private static readonly ColumnInfo Col = new("c", "int", typeof(int), true, 0);

    private sealed class FakeReader(bool grid, int rows, long affected) : IResultReader
    {
        private bool _done;
        public bool Disposed { get; private set; }
        public IReadOnlyList<ColumnInfo> Columns { get; } = grid ? [Col] : [];
        public long RecordsAffected => affected;

        public ValueTask<object?[][]?> ReadBatchAsync(int maxRows, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_done)
                return ValueTask.FromResult<object?[][]?>(null);
            _done = true;
            return ValueTask.FromResult<object?[][]?>([.. Enumerable.Range(0, rows).Select(i => new object?[] { i })]);
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class MultiReader(int[] sets, string[] messages) : IResultReader
    {
        private int _set;
        private bool _read;
        public IReadOnlyList<ColumnInfo> Columns => _set < sets.Length && sets[_set] >= 0 ? [Col] : [];
        public long RecordsAffected => -1;
        public IReadOnlyList<string> Messages => messages;

        public ValueTask<object?[][]?> ReadBatchAsync(int maxRows, CancellationToken cancellationToken)
        {
            if (_read || _set >= sets.Length)
                return ValueTask.FromResult<object?[][]?>(null);
            _read = true;
            return ValueTask.FromResult<object?[][]?>([.. Enumerable.Range(0, Math.Max(sets[_set], 0)).Select(i => new object?[] { i })]);
        }

        public ValueTask<bool> NextResultAsync(CancellationToken cancellationToken)
        {
            _read = false;
            return ValueTask.FromResult(++_set < sets.Length);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeConnection : IDatabaseConnection
    {
        public List<string> Executed { get; } = [];
        public List<IReadOnlyList<QueryParameter>> Bound { get; } = [];
        public List<FakeReader> Readers { get; } = [];
        public Func<string, CancellationToken, Task>? Gate { get; set; }
        public int SchemaReads { get; private set; }
        public long? AffectedOverride { get; set; }
        public string ServerVersion => "1";

        public async ValueTask<IResultReader> ExecuteAsync(string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken)
        {
            Executed.Add(sql);
            Bound.Add(parameters);
            if (Gate is not null)
                await Gate(sql, cancellationToken);
            if (sql.Contains("slowquery", StringComparison.Ordinal))
                throw new TimeoutException("Command timeout expired");
            if (sql.Contains("boom", StringComparison.Ordinal))
                throw new InvalidOperationException("boom failed");
            if (sql.Contains("multi", StringComparison.Ordinal))
                return new MultiReader([2, 4], ["hello", "world"]);
            if (sql.Contains("print", StringComparison.Ordinal))
                return new MultiReader([-1], ["only a message"]);
            var reader = sql.StartsWith("SELECT", StringComparison.Ordinal)
                ? new FakeReader(true, 3, -1)
                : new FakeReader(false, 0, sql.StartsWith("UPDATE", StringComparison.Ordinal) ? AffectedOverride ?? 5 : -1);
            Readers.Add(reader);
            return reader;
        }

        public ValueTask<SchemaSnapshot> ReadSchemaAsync(CancellationToken cancellationToken)
        {
            SchemaReads++;
            return ValueTask.FromResult(SchemaSnapshot.Empty);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeHistory : IQueryHistoryStore
    {
        public List<HistoryEntry> Entries { get; } = [];

        public ValueTask AppendAsync(HistoryEntry e, CancellationToken ct)
        {
            Entries.Add(e);
            return ValueTask.CompletedTask;
        }

        public ValueTask<IReadOnlyList<HistoryEntry>> ListAsync(int max, CancellationToken ct) => ValueTask.FromResult<IReadOnlyList<HistoryEntry>>(Entries);

        public ValueTask ClearAsync(CancellationToken ct) => ValueTask.CompletedTask;
    }

    private sealed class FakeSettings : ISettingsStore
    {
        public ValueTask<AppSettings> LoadAsync(CancellationToken ct) => ValueTask.FromResult(AppSettings.Default);

        public ValueTask SaveAsync(AppSettings settings, CancellationToken ct) => ValueTask.CompletedTask;
    }

    private sealed class Fixture
    {
        public Fixture(string script, DatabaseDialect dialect = DatabaseDialect.PostgreSql)
        {
            Connection = new FakeConnection();
            Editor = new StubSqlEditor { Text = script };
            var session = new ConnectionSession(
                new ConnectionProfile(Guid.NewGuid(), "p", dialect, "h", 1, "d", "u", null, null),
                Connection, SchemaSnapshot.Empty, null);
            Vm = new QueryTabViewModel(session, Editor, History, new FakeSettings(), new ImmediateDispatcher(), Dialogs);
        }

        public FakeConnection Connection { get; }
        public FakeDialogService Dialogs { get; } = new();
        public StubSqlEditor Editor { get; }
        public FakeHistory History { get; } = new();
        public QueryTabViewModel Vm { get; }
    }

    [Fact]
    public async Task Run_SingleStatement_ProducesGridAndHistory()
    {
        var f = new Fixture("SELECT 1;");

        await f.Vm.RunCommand.ExecuteAsync(null);

        var result = Assert.Single(f.Vm.Results);
        Assert.Equal(ResultKind.Grid, result.Kind);
        Assert.Equal("Résultat 1", result.Title);
        Assert.Equal(3, result.Grid!.RowCount);
        Assert.Same(result, f.Vm.SelectedResult);
        Assert.False(f.Vm.IsRunning);
        Assert.True(f.Connection.Readers[0].Disposed);
        var entry = Assert.Single(f.History.Entries);
        Assert.True(entry.Succeeded);
        Assert.Equal(3, entry.RowCount);
        Assert.Equal("SELECT 1;", entry.Sql);
    }

    [Fact]
    public async Task RunAll_ThreeStatements_RunsInOrder()
    {
        var f = new Fixture("SELECT 1; SELECT 2; SELECT 3;");

        await f.Vm.RunAllCommand.ExecuteAsync(null);

        Assert.Equal(["Résultat 1", "Résultat 2", "Résultat 3"], f.Vm.Results.Select(r => r.Title));
        Assert.Equal(3, f.History.Entries.Count);
        Assert.Equal(3, f.Connection.Executed.Count);
    }

    [Fact]
    public async Task Run_CommandTimeout_ShowsDriverMessageAndReenablesRun()
    {
        var f = new Fixture("SELECT slowquery;");

        await f.Vm.RunCommand.ExecuteAsync(null);

        Assert.Equal("Command timeout expired", Assert.Single(f.Vm.Results).Text);
        Assert.False(f.Vm.IsRunning);
        Assert.NotEqual(UiText.Get("query.running"), f.Vm.StatusText);
        Assert.True(f.Vm.RunCommand.CanExecute(null));
        Assert.False(f.History.Entries.Single().Succeeded);
    }

    [Fact]
    public async Task RunAll_ErrorInSecond_StopsAndKeepsPreviousResults()
    {
        var f = new Fixture("SELECT 1; SELECT boom; SELECT 3;");

        await f.Vm.RunAllCommand.ExecuteAsync(null);

        Assert.Equal(2, f.Vm.Results.Count);
        Assert.Equal(ResultKind.Grid, f.Vm.Results[0].Kind);
        Assert.Equal(ResultKind.Message, f.Vm.Results[1].Kind);
        Assert.Equal("boom failed", f.Vm.Results[1].Text);
        Assert.Equal(2, f.Connection.Executed.Count);
        Assert.Equal([true, false], f.History.Entries.Select(e => e.Succeeded));
    }

    [Fact]
    public async Task Run_TwoSetsAndMessages_ShowsEverySetThenMessages()
    {
        using var _ = UiText.Override(UiLanguage.English);
        var f = new Fixture("SELECT multi;");

        await f.Vm.RunCommand.ExecuteAsync(null);

        Assert.Equal(["Result 1 (1/2)", "Result 1 (2/2)", "Messages"], f.Vm.Results.Select(r => r.Title));
        Assert.Equal([ResultKind.Grid, ResultKind.Grid, ResultKind.Messages], f.Vm.Results.Select(r => r.Kind));
        Assert.Equal([2, 4], f.Vm.Results.Take(2).Select(r => (int)r.Grid!.RowCount));
        Assert.Equal("hello" + Environment.NewLine + "world", f.Vm.Results[2].Text);
        Assert.All(f.Vm.Results.Take(2), r => Assert.NotNull(r.ReadOnlyReason));
        Assert.Equal(2, f.History.Entries.Single().RowCount);
    }

    [Fact]
    public async Task Run_MessagesOnly_ShowsCompletionAndMessages()
    {
        var f = new Fixture("print 1;");

        await f.Vm.RunCommand.ExecuteAsync(null);

        Assert.Equal([ResultKind.Message, ResultKind.Messages], f.Vm.Results.Select(r => r.Kind));
        Assert.Equal("only a message", f.Vm.Results[1].Text);
    }

    [Fact]
    public async Task Run_Dml_ShowsRowsAffectedMessage()
    {
        var f = new Fixture("UPDATE t SET a = 1;");

        await f.Vm.RunCommand.ExecuteAsync(null);

        Assert.Equal("5 lignes affectées", Assert.Single(f.Vm.Results).Text);
        Assert.Equal(5, f.History.Entries[0].RowCount);
    }

    [Theory]
    [InlineData(UiLanguage.French, 1, "1 ligne affectée")]
    [InlineData(UiLanguage.French, 5, "5 lignes affectées")]
    [InlineData(UiLanguage.English, 1, "1 row affected")]
    [InlineData(UiLanguage.English, 5, "5 rows affected")]
    public async Task Run_Dml_RowsAffectedIsPluralAndLocalized(UiLanguage language, int rows, string expected)
    {
        using var _ = UiText.Override(language);
        var f = new Fixture("UPDATE t SET a = 1;");
        f.Connection.AffectedOverride = rows;

        await f.Vm.RunCommand.ExecuteAsync(null);

        Assert.Equal(expected, Assert.Single(f.Vm.Results).Text);
    }

    [Theory]
    [InlineData(UiLanguage.French, "Exécuter la requête", "Abandonner les modifications en attente de la grille ?", "Annulé")]
    [InlineData(UiLanguage.English, "Run query", "Discard pending grid changes?", "Cancelled")]
    public void DiscardConfirmationAndCancelStatus_ComeFromTheCatalog(UiLanguage language, string title, string message, string status)
    {
        using var _ = UiText.Override(language);

        Assert.Equal(title, UiText.Get("query.run.title"));
        Assert.Equal(message, UiText.Get("query.run.discardChanges"));
        Assert.Equal(status, UiText.Get("query.cancelled"));
    }

    [Theory]
    [InlineData(UiLanguage.French, "2 instructions exécutées en ")]
    [InlineData(UiLanguage.English, "Completed 2 statements in ")]
    public async Task Run_Script_StatusIsLocalized(UiLanguage language, string prefix)
    {
        using var _ = UiText.Override(language);
        var f = new Fixture("SELECT 1; SELECT 2;");

        await f.Vm.RunAllCommand.ExecuteAsync(null);

        Assert.Contains(prefix, f.Vm.StatusText, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(UiLanguage.French, "Commande exécutée")]
    [InlineData(UiLanguage.English, "Command completed")]
    public async Task Run_StatementWithoutCount_CommandCompletedIsLocalized(UiLanguage language, string expected)
    {
        using var _ = UiText.Override(language);
        var f = new Fixture("TRUNCATE t;");

        await f.Vm.RunCommand.ExecuteAsync(null);

        Assert.Equal(expected, Assert.Single(f.Vm.Results).Text);
    }

    [Theory]
    [InlineData(UiLanguage.French)]
    [InlineData(UiLanguage.English)]
    public async Task Run_EngineError_IsShownVerbatim(UiLanguage language)
    {
        using var _ = UiText.Override(language);
        var f = new Fixture("boom;");

        await f.Vm.RunCommand.ExecuteAsync(null);

        Assert.Equal("boom failed", Assert.Single(f.Vm.Results).Text);
    }

    [Theory]
    [InlineData(UiLanguage.French, "Résultat 1")]
    [InlineData(UiLanguage.English, "Result 1")]
    public async Task Run_Grid_TitleIsLocalized(UiLanguage language, string expected)
    {
        using var _ = UiText.Override(language);
        var f = new Fixture("SELECT 1;");

        await f.Vm.RunCommand.ExecuteAsync(null);

        Assert.Equal(expected, Assert.Single(f.Vm.Results).Title);
    }

    [Theory]
    [InlineData(UiLanguage.French, "Table 'nope' inconnue")]
    [InlineData(UiLanguage.English, "Unknown table 'nope'")]
    public void Diagnostics_UnknownTable_AreRenderedInTheCurrentLanguage(UiLanguage language, string expected)
    {
        using var _ = UiText.Override(language);
        var f = new Fixture("SELECT * FROM nope");

        Assert.Contains(f.Editor.Diagnostics, d => d.Message.Contains(expected, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Run_StatementWithoutCount_ShowsCommandCompleted()
    {
        var f = new Fixture("TRUNCATE t;");

        await f.Vm.RunCommand.ExecuteAsync(null);

        Assert.Equal("Commande exécutée", Assert.Single(f.Vm.Results).Text);
    }

    [Fact]
    public async Task Run_Ddl_RefreshesSchema()
    {
        var f = new Fixture("CREATE TABLE t (a int);");

        await f.Vm.RunCommand.ExecuteAsync(null);

        Assert.Equal(1, f.Connection.SchemaReads);
    }

    [Fact]
    public async Task Cancel_WhileRunning_StopsTheRun()
    {
        var f = new Fixture("SELECT 1; SELECT 2;");
        var started = new TaskCompletionSource();
        f.Connection.Gate = async (_, ct) =>
        {
            started.TrySetResult();
            await Task.Delay(Timeout.Infinite, ct);
        };

        var run = f.Vm.RunAllCommand.ExecuteAsync(null);
        await started.Task;
        Assert.True(f.Vm.IsRunning);
        f.Vm.CancelCommand.Execute(null);
        await run;

        Assert.False(f.Vm.IsRunning);
        Assert.Equal("Annulé", f.Vm.StatusText);
        Assert.Single(f.Connection.Executed);
        Assert.False(Assert.Single(f.History.Entries).Succeeded);
    }

    [Fact]
    public async Task Run_WhileRunning_IsIgnored()
    {
        var f = new Fixture("SELECT 1;");
        var release = new TaskCompletionSource();
        var started = new TaskCompletionSource();
        f.Connection.Gate = async (_, _) =>
        {
            started.TrySetResult();
            await release.Task;
        };

        var first = f.Vm.RunCommand.ExecuteAsync(null);
        await started.Task;
        await f.Vm.RunCommand.ExecuteAsync(null);
        release.SetResult();
        await first;

        Assert.Single(f.Connection.Executed);
        Assert.Single(f.Vm.Results);
    }

    [Fact]
    public async Task Run_Again_ReplacesPreviousResults()
    {
        var f = new Fixture("SELECT 1;");

        await f.Vm.RunCommand.ExecuteAsync(null);
        await f.Vm.RunCommand.ExecuteAsync(null);

        Assert.Single(f.Vm.Results);
        Assert.Equal(2, f.History.Entries.Count);
    }

    [Fact]
    public void Constructor_WiresEditor()
    {
        var f = new Fixture("SELECT 1;");

        Assert.Equal(DatabaseDialect.PostgreSql, f.Editor.Dialect);
        Assert.NotNull(f.Editor.CompletionProvider);
        Assert.NotNull(f.Editor.HoverProvider);
    }

    private static IReadOnlyList<ParameterValue> Values(IReadOnlyList<string> names, string text) =>
        [.. names.Select(n => new ParameterValue(n, text, false))];

    [Fact]
    public async Task Run_NamedParameters_PromptsOnceAndBindsRewrittenSql()
    {
        var f = new Fixture("SELECT :a, $b; SELECT :a;", DatabaseDialect.Sqlite);
        f.Dialogs.ParametersResult = (names, _) => [.. Values(names, "x").Where(v => v.Name != "b"), new("b", null, true)];

        await f.Vm.RunAllCommand.ExecuteAsync(null);

        Assert.Equal(["SELECT @a, @b;", "SELECT @a;"], f.Connection.Executed);
        Assert.Single(f.Dialogs.ParameterPrompts);
        Assert.Equal([new("a", "x"), new("b", null)], f.Connection.Bound[0]);
        Assert.Equal([new("a", "x")], f.Connection.Bound[1]);
        Assert.Equal("SELECT :a, $b;", f.History.Entries[0].Sql);
    }

    [Fact]
    public async Task Run_PromptCancelled_RunsNothing()
    {
        var f = new Fixture("SELECT :a;", DatabaseDialect.Sqlite);

        await f.Vm.RunCommand.ExecuteAsync(null);

        Assert.Empty(f.Connection.Executed);
        Assert.Empty(f.Vm.Results);
        Assert.Equal("Annulé", f.Vm.StatusText);
        Assert.False(f.Vm.IsRunning);
    }

    [Fact]
    public async Task Run_SecondTime_OffersLastValuesAsDefaults()
    {
        var f = new Fixture("SELECT :a;", DatabaseDialect.Sqlite);
        f.Dialogs.ParametersResult = (names, _) => Values(names, "x");

        await f.Vm.RunCommand.ExecuteAsync(null);
        await f.Vm.RunCommand.ExecuteAsync(null);

        Assert.Empty(f.Dialogs.ParameterPrompts[0]);
        Assert.Equal("x", f.Dialogs.ParameterPrompts[1]["a"].Text);
    }

    [Fact]
    public async Task Run_NoParameters_DoesNotPrompt()
    {
        var f = new Fixture("SELECT 1;", DatabaseDialect.Sqlite);

        await f.Vm.RunCommand.ExecuteAsync(null);

        Assert.Empty(f.Dialogs.ParameterPrompts);
    }

    [Fact]
    public async Task Run_PositionalParameter_ShowsMessageAndSkipsExecution()
    {
        var f = new Fixture("SELECT ?;", DatabaseDialect.Sqlite);

        await f.Vm.RunCommand.ExecuteAsync(null);

        Assert.Empty(f.Connection.Executed);
        Assert.Equal("Les paramètres positionnels ne sont pas pris en charge, utilisez :nom", Assert.Single(f.Vm.Results).Text);
    }
}
