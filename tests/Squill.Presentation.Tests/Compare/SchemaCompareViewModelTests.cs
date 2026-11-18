using Squill.Core;
using Squill.Core.Schema.Compare;
using Squill.Data;
using Squill.Presentation.Compare;
using Squill.Presentation.Localization;
using Squill.Presentation.Tests.Panels;
using Squill.Presentation.Sessions;
using Squill.Sql.Migration;
using Xunit;

namespace Squill.Presentation.Tests.Compare;

public sealed class SchemaCompareViewModelTests
{
    private static ColumnDefinition Col(string name, bool pk = false) => new(name, "integer", !pk, pk, null);

    private static TableInfo Table(string name, params string[] columns) =>
        new("public", name, TableKind.Table, [Col("id", true), .. columns.Select(c => Col(c))], [], []);

    private static SchemaSnapshot Snapshot(params TableInfo[] tables) => new("public", tables);

    // An excluded removal stays in the script as a comment.
    private static bool Drops(string script) => script.Split('\n').Any(l => l.StartsWith("DROP TABLE", StringComparison.Ordinal));

    private sealed class FakeReader : IResultReader
    {
        public IReadOnlyList<ColumnInfo> Columns => [];
        public long RecordsAffected => -1;
        public ValueTask<object?[][]?> ReadBatchAsync(int maxRows, CancellationToken cancellationToken) => ValueTask.FromResult<object?[][]?>(null);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeConnection(SchemaSnapshot schema) : IDatabaseConnection
    {
        public List<string> Executed { get; } = [];
        public SchemaSnapshot Next { get; set; } = schema;
        public string? FailOn { get; set; }
        public Func<SchemaSnapshot>? AfterExecute { get; set; }
        public TaskCompletionSource? ReadGate { get; set; }
        public string ServerVersion => "16.1";

        public ValueTask<IResultReader> ExecuteAsync(string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken)
        {
            Executed.Add(sql);
            if (FailOn is not null && sql.Contains(FailOn, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("boom failed");
            if (AfterExecute is not null && !sql.Contains("BEGIN", StringComparison.OrdinalIgnoreCase) && !sql.Contains("COMMIT", StringComparison.OrdinalIgnoreCase))
                Next = AfterExecute();
            return ValueTask.FromResult<IResultReader>(new FakeReader());
        }

        public ValueTask<IReadOnlyList<string>> ListDatabasesAsync(CancellationToken cancellationToken) => ValueTask.FromResult<IReadOnlyList<string>>([]);

        public async ValueTask<SchemaSnapshot> ReadSchemaAsync(CancellationToken cancellationToken)
        {
            if (ReadGate is { } gate)
                await gate.Task.WaitAsync(cancellationToken);
            return Next;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class Fixture
    {
        public Fixture(ConnectionEnvironment targetEnvironment = ConnectionEnvironment.None, DatabaseDialect dialect = DatabaseDialect.PostgreSql, TimeSpan? debounce = null, SchemaCompareViewModel.ScriptBuilder? build = null)
        {
            // Source: a (id, name, note), b. Target: a (id, name), old.
            SourceConnection = new FakeConnection(Snapshot(Table("a", "name", "note"), Table("b")));
            TargetConnection = new FakeConnection(Snapshot(Table("a", "name"), Table("old")));
            Source = new ConnectionSession(new ConnectionProfile(Guid.NewGuid(), "dev", dialect, "h", 1, "devdb", "u", null, null), SourceConnection, SourceConnection.Next, null);
            Target = new ConnectionSession(new ConnectionProfile(Guid.NewGuid(), "prod", dialect, "h", 1, "proddb", "u", null, null, targetEnvironment), TargetConnection, TargetConnection.Next, null);
            Vm = new SchemaCompareViewModel(() => [Source, Target, .. Extra], Dialogs, null, debounce ?? TimeSpan.Zero, build ?? MigrationScriptBuilder.Build);
            Vm.SelectSource(Source);
        }

        public List<ConnectionSession> Extra { get; } = [];
        public FakeConnection SourceConnection { get; }
        public FakeConnection TargetConnection { get; }
        public ConnectionSession Source { get; }
        public ConnectionSession Target { get; }
        public FakeDialogService Dialogs { get; } = new();
        public SchemaCompareViewModel Vm { get; }

        public async Task CompareAsync()
        {
            await Vm.CompareCommand.ExecuteAsync(null);
            await Vm.ScriptTask;
        }
    }

    [Fact]
    public async Task Compare_ListsDifferencesWithCountsAndDefaults()
    {
        using var _ = UiText.Override(UiLanguage.French);
        var f = new Fixture();

        await f.CompareAsync();

        Assert.True(CompareState.Ready == f.Vm.State, f.Vm.FailureText);
        Assert.Equal((1, 1, 1), (f.Vm.AddedCount, f.Vm.ModifiedCount, f.Vm.RemovedCount));
        var group = Assert.Single(f.Vm.Groups);
        Assert.Equal("TABLES", group.Title);
        Assert.Equal(["public.a", "public.b", "public.old"], group.Items.Select(i => i.Name));
        Assert.Equal(["~", "+", "-"], group.Items.Select(i => i.Glyph));
        Assert.Equal([true, true, false], group.Items.Select(i => i.IsIncluded));
        Assert.Equal("1 colonne", group.Items[0].Summary);
        Assert.Equal("nouvelle", group.Items[1].Summary);
        Assert.Equal("absente source", group.Items[2].Summary);
        Assert.Equal("Tout inclure · 2 sur 3 sélectionnés", f.Vm.SelectionText);
        Assert.Equal("3 différences · 2 incluses", f.Vm.DifferencesText);
        Assert.Equal("16.1", f.Vm.SourceServerVersion);
        Assert.Equal("Source · dev · devdb", f.Vm.SourceCaption);
        Assert.Contains("a", f.Vm.Script, StringComparison.Ordinal);
        Assert.False(Drops(f.Vm.Script));
    }

    [Fact]
    public async Task StatusBar_ShowsTheEnvironmentFlow()
    {
        var f = new Fixture(ConnectionEnvironment.Production);

        await f.CompareAsync();

        Assert.Equal(" -> PRODUCTION", f.Vm.EnvironmentFlow);
        Assert.Equal(ConnectionEnvironment.Production, f.Vm.Environment);
    }

    [Fact]
    public async Task Chips_AndFilterBox_NarrowTheList()
    {
        var f = new Fixture();
        await f.CompareAsync();

        f.Vm.ShowRemoved = false;
        Assert.Equal(2, f.Vm.Groups[0].Count);
        f.Vm.ShowAdded = false;
        Assert.Equal("public.a", Assert.Single(f.Vm.Groups[0].Items).Name);
        f.Vm.ShowAdded = f.Vm.ShowRemoved = true;
        f.Vm.FilterText = "OLD";
        Assert.Equal("public.old", Assert.Single(f.Vm.Groups[0].Items).Name);
        f.Vm.FilterText = "zzz";
        Assert.Empty(f.Vm.Groups);
        Assert.Equal(3, f.Vm.TotalCount);
    }

    [Fact]
    public async Task IncludingARemoval_RebuildsTheScript()
    {
        var f = new Fixture();
        await f.CompareAsync();
        Assert.False(Drops(f.Vm.Script));

        f.Vm.Groups[0].Items[2].IsIncluded = true;
        await f.Vm.ScriptTask;

        Assert.True(Drops(f.Vm.Script));
        Assert.Equal(3, f.Vm.IncludedCount);
        Assert.NotEmpty(f.Vm.ScriptNotes);
        f.Vm.SetAllIncludedCommand.Execute(false);
        await f.Vm.ScriptTask;
        Assert.Equal(0, f.Vm.IncludedCount);
        Assert.False(f.Vm.AllIncluded);
        Assert.False(f.Vm.ExecuteScriptCommand.CanExecute(null));
    }

    [Fact]
    public async Task StaleScriptBuild_NeverWins()
    {
        using var gate = new ManualResetEventSlim();
        var calls = 0;
        MigrationScript Build(DatabaseDialect d, SchemaSnapshot s, SchemaSnapshot t, SchemaComparison c, IReadOnlySet<ObjectKey> included, MigrationOptions o)
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                gate.Wait();
                return new MigrationScript("first", 1, []);
            }

            return new MigrationScript("second", 1, []);
        }

        var f = new Fixture(build: Build);
        await f.Vm.CompareCommand.ExecuteAsync(null);
        var first = f.Vm.ScriptTask;
        f.Vm.Groups[0].Items[2].IsIncluded = true;
        await f.Vm.ScriptTask;
        Assert.Equal("second", f.Vm.Script);

        gate.Set();
        await first;

        Assert.Equal("second", f.Vm.Script);
        Assert.False(f.Vm.IsBuilding);
    }

    [Fact]
    public async Task EditedScript_IsKeptUntilRegenerate()
    {
        var f = new Fixture();
        await f.CompareAsync();
        var generated = f.Vm.Script;

        f.Vm.Script = "-- mine";
        Assert.True(f.Vm.IsScriptEdited);
        f.Vm.Groups[0].Items[2].IsIncluded = true;
        await f.Vm.ScriptTask;
        Assert.Equal("-- mine", f.Vm.Script);

        f.Vm.RegenerateCommand.Execute(null);
        Assert.False(f.Vm.IsScriptEdited);
        Assert.True(Drops(f.Vm.Script));
        Assert.NotEqual(generated, f.Vm.Script);
    }

    [Fact]
    public async Task SelectingAnObject_ShowsBothDdlsAndJumpsBetweenDifferences()
    {
        var f = new Fixture();
        await f.CompareAsync();

        var selected = f.Vm.SelectedItem;
        Assert.Equal("public.a", selected?.Name);
        Assert.Contains("note", f.Vm.SourceText, StringComparison.Ordinal);
        Assert.DoesNotContain("note", f.Vm.TargetText, StringComparison.Ordinal);
        Assert.Contains(f.Vm.Lines, l => l.Mark != LineMark.Same);

        f.Vm.NextDifferenceCommand.Execute(null);
        var first = f.Vm.CurrentDifferenceLine;
        Assert.NotEqual(LineMark.Same, f.Vm.Lines[first].Mark);
        f.Vm.PreviousDifferenceCommand.Execute(null);
        Assert.NotEqual(LineMark.Same, f.Vm.Lines[f.Vm.CurrentDifferenceLine].Mark);
    }

    [Fact]
    public async Task Swap_ExchangesTheSidesAndDiscardsTheResult()
    {
        var f = new Fixture();
        await f.CompareAsync();

        f.Vm.SwapCommand.Execute(null);

        Assert.Equal(f.Target, f.Vm.Source?.Session);
        Assert.Equal(f.Source, f.Vm.Target?.Session);
        Assert.Equal(CompareState.Idle, f.Vm.State);
        Assert.Empty(f.Vm.Groups);
        Assert.Equal("", f.Vm.Script);
    }

    [Fact]
    public void Targets_ListOnlyTheEngineOfTheSource()
    {
        var f = new Fixture();
        var file = new ConnectionSession(new ConnectionProfile(Guid.NewGuid(), "file", DatabaseDialect.Sqlite, null, null, null, null, ":memory:", null), new FakeConnection(Snapshot()), Snapshot(), null);
        var oracle = new ConnectionSession(new ConnectionProfile(Guid.NewGuid(), "ora", DatabaseDialect.Oracle, "h", 1, "o", "u", null, null), new FakeConnection(Snapshot()), Snapshot(), null);
        f.Extra.AddRange([file, oracle]);

        f.Vm.RefreshEndpoints();

        Assert.Equal(f.Target, Assert.Single(f.Vm.Targets).Session);
        Assert.DoesNotContain(f.Vm.Sources, e => e.Dialect == DatabaseDialect.Oracle);
        Assert.Contains(f.Vm.Sources, e => e.Session == file);
    }

    [Fact]
    public async Task Cancel_WhileComparing_LeavesNothingBehind()
    {
        var f = new Fixture();
        f.TargetConnection.ReadGate = new TaskCompletionSource();

        var running = f.Vm.CompareCommand.ExecuteAsync(null);
        Assert.Equal(CompareState.Comparing, f.Vm.State);
        f.Vm.CancelCompareCommand.Execute(null);
        await running;

        Assert.Equal(CompareState.Idle, f.Vm.State);
        Assert.Empty(f.Vm.Groups);
        Assert.Equal("", f.Vm.FailureText);
    }

    [Fact]
    public async Task Compare_Failure_IsReportedWithoutThrowing()
    {
        var f = new Fixture();
        f.TargetConnection.ReadGate = new TaskCompletionSource();
        f.TargetConnection.ReadGate.SetException(new InvalidOperationException("no server"));

        await f.Vm.CompareCommand.ExecuteAsync(null);

        Assert.Equal(CompareState.Failed, f.Vm.State);
        Assert.Contains("no server", f.Vm.FailureText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Execute_OnAProductionTarget_AsksAndRunsNothingWhenRefused()
    {
        var f = new Fixture(ConnectionEnvironment.Production);
        await f.CompareAsync();
        f.Vm.Groups[0].Items[2].IsIncluded = true; // a DROP: the confirmation asks for the typed word
        await f.Vm.ScriptTask;
        f.Dialogs.ProductionResult = new(false, false);

        await f.Vm.ExecuteScriptCommand.ExecuteAsync(null);

        var request = Assert.Single(f.Dialogs.ProductionRequests);
        Assert.Equal("proddb", request.RequiredText);
        Assert.Empty(f.TargetConnection.Executed);
        Assert.Empty(f.Dialogs.Errors);
        Assert.Equal("", f.Vm.ExecuteSummary);
    }

    [Fact]
    public async Task Execute_OnAProductionTarget_NeverSkipsTheConfirmation()
    {
        var f = new Fixture(ConnectionEnvironment.Production);
        await f.CompareAsync();
        f.Dialogs.ProductionResult = new(false, false);

        await f.Vm.ExecuteScriptCommand.ExecuteAsync(null);
        f.Vm.Script = "ALTER TABLE a ADD COLUMN x integer;";
        await f.Vm.ExecuteScriptCommand.ExecuteAsync(null);

        Assert.Equal(2, f.Dialogs.ProductionRequests.Count);
        Assert.Empty(f.TargetConnection.Executed);
    }

    [Fact]
    public async Task Execute_Success_RunsTheStatementsWithoutTheScriptTransactionAndComparesAgain()
    {
        var f = new Fixture();
        f.TargetConnection.AfterExecute = () => f.SourceConnection.Next;
        await f.CompareAsync();
        f.Vm.Groups[0].Items[2].IsIncluded = false;
        var before = f.Vm.ComparedAt;

        await f.Vm.ExecuteScriptCommand.ExecuteAsync(null);

        Assert.Equal("", f.Vm.ExecuteError);
        Assert.NotEqual("", f.Vm.ExecuteSummary);
        Assert.Contains(f.TargetConnection.Executed, s => s.Contains("CREATE TABLE", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(f.TargetConnection.Executed, s => s.Contains("ALTER TABLE", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("BEGIN", f.TargetConnection.Executed[0]); // the scope's own transaction
        Assert.Equal("COMMIT", f.TargetConnection.Executed[^1]);
        Assert.Equal(1, f.TargetConnection.Executed.Count(s => s == "BEGIN"));
        Assert.Equal(CompareState.Ready, f.Vm.State);
        Assert.Equal(0, f.Vm.TotalCount);
        Assert.NotNull(before);
        Assert.Empty(f.Dialogs.ProductionRequests);
    }

    [Fact]
    public async Task Execute_Failure_KeepsTheScriptAndShowsTheFailingStatement()
    {
        var f = new Fixture();
        f.TargetConnection.FailOn = "CREATE TABLE";
        await f.CompareAsync();
        var script = f.Vm.Script;

        await f.Vm.ExecuteScriptCommand.ExecuteAsync(null);

        Assert.Equal(script, f.Vm.Script);
        Assert.Equal(CompareState.Ready, f.Vm.State);
        Assert.Contains("boom failed", f.Vm.ExecuteError, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE", f.Vm.ExecuteError, StringComparison.OrdinalIgnoreCase);
        Assert.Single(f.Dialogs.Errors);
        Assert.Contains("ROLLBACK", f.TargetConnection.Executed);
        Assert.Equal("", f.Vm.ExecuteSummary);
    }

    [Fact]
    public async Task Execute_OnAMySqlTarget_AsksBeforeRunning()
    {
        var f = new Fixture(dialect: DatabaseDialect.MySql);
        await f.CompareAsync();
        f.Dialogs.ConfirmResult = false;

        await f.Vm.ExecuteScriptCommand.ExecuteAsync(null);

        Assert.Single(f.Dialogs.Confirms);
        Assert.Empty(f.TargetConnection.Executed);
    }

    [Fact]
    public async Task OpenInEditor_HandsTheScriptToTheTargetSession()
    {
        var f = new Fixture();
        await f.CompareAsync();
        (ConnectionSession Session, string Text)? requested = null;
        f.Vm.ScriptRequested += (s, t) => requested = (s, t);
        string? copied = null;
        f.Vm.CopyRequested += t => copied = t;

        f.Vm.OpenInEditorCommand.Execute(null);
        f.Vm.CopyCommand.Execute(null);

        Assert.Equal(f.Target, requested?.Session);
        Assert.Equal(f.Vm.Script, requested?.Text);
        Assert.Equal(f.Vm.Script, copied);
    }

    [Fact]
    public async Task SaveAs_WritesTheScript()
    {
        var f = new Fixture();
        await f.CompareAsync();
        var path = Path.Combine(Path.GetTempPath(), $"squill-compare-{Guid.NewGuid():N}.sql");
        f.Dialogs.SavePathResult = path;
        try
        {
            await f.Vm.SaveAsCommand.ExecuteAsync(null);

            Assert.Equal(f.Vm.Script, await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Endpoints_NeedTwoConnectionsOfOneEngine()
    {
        var f = new Fixture();
        var sqlite = new ConnectionSession(new ConnectionProfile(Guid.NewGuid(), "file", DatabaseDialect.Sqlite, null, null, null, null, ":memory:", null), new FakeConnection(Snapshot()), Snapshot(), null);

        Assert.True(CompareEndpoint.CanCompare(CompareEndpoint.List([f.Source, f.Target])));
        Assert.False(CompareEndpoint.CanCompare(CompareEndpoint.List([f.Source, sqlite])));
        Assert.False(CompareEndpoint.CanCompare(CompareEndpoint.List([f.Source])));
    }

    [Fact]
    public void Workspace_OpensTheTabOnlyWithTwoConnectionsOfOneEngine()
    {
        var f = new Fixture();
        var host = new PanelsHost();
        var workspace = host.Workspace;
        Assert.False(workspace.OpenSchemaCompareCommand.CanExecute(null));
        Assert.Equal(UiText.Get("compare.disabled.sessions"), workspace.SchemaCompareDisabledReason);

        workspace.Sessions.Add(f.Source);
        Assert.False(workspace.OpenSchemaCompareCommand.CanExecute(null));
        workspace.Sessions.Add(f.Target);
        Assert.True(workspace.OpenSchemaCompareCommand.CanExecute(null));
        Assert.Equal("", workspace.SchemaCompareDisabledReason);

        workspace.OpenSchemaCompare(f.Source.Profile.Id);
        workspace.OpenSchemaCompare(f.Source.Profile.Id);

        var tab = Assert.Single(workspace.Compares);
        Assert.Same(tab, workspace.ActiveCompare);
        Assert.Equal(f.Source, tab.Source?.Session);
        Assert.False(workspace.IsHomeVisible);
        tab.CloseCommand.Execute(null);
        Assert.Empty(workspace.Compares);
        Assert.Null(workspace.ActiveCompare);
    }
}
