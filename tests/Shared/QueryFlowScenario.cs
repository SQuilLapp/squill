using System.Globalization;
using Squill.Core;
using Squill.Data;
using Squill.Platform.Profiles;
using Squill.Platform.Settings;
using Squill.Platform.Ssh;
using Squill.Platform.Storage;
using Squill.Presentation;
using Squill.Presentation.Editor;
using Squill.Presentation.Localization;
using Squill.Presentation.Query;
using Squill.Presentation.Results;
using Squill.Presentation.Sessions;
using Squill.Sql.Completion;
using Squill.Sql.Ddl;
using Xunit;

namespace Squill.Data.Tests.Scenarios;

/// <summary>Engine-specific statements of <see cref="QueryFlowScenario"/>.</summary>
/// <param name="HugeSql">A statement returning exactly 100,000 rows of one integer column.</param>
/// <param name="SlowSql">A statement that runs for at least 20 s unless cancelled.</param>
/// <param name="Sigil">Prefix of a named parameter in the engine: <c>:</c> for SQLite, <c>@</c> for the others.</param>
/// <param name="Positional">The positional placeholder of the engine (<c>?</c>, <c>$1</c>), or <c>null</c> when it has none.</param>
/// <param name="CompletionText">Text whose end asks for the completion of <c>qf_t</c> in the engine's identifier style.</param>
/// <param name="CompletionLabel">Label the completion must offer for <paramref name="CompletionText"/>.</param>
public sealed record QueryFlowSpec(string HugeSql, string SlowSql, string Sigil, string? Positional, string CompletionText, string CompletionLabel);

/// <summary>Drives what a user does in a query tab (several statements, run under the cursor, a failure, parameters, cancel, 100k rows, export, completion) through the real view models on a live connection.</summary>
public static class QueryFlowScenario
{
    private static CancellationToken Ct => CancellationToken.None;

    /// <summary>Runs every case against one engine.</summary>
    /// <param name="provider">The provider of the engine under test.</param>
    /// <param name="profile">Profile of an empty, writable database.</param>
    /// <param name="password">The password of the profile, or <c>null</c>.</param>
    /// <param name="spec">Statements that differ per engine.</param>
    public static async Task RunAsync(IDatabaseProvider provider, ConnectionProfile profile, string? password, QueryFlowSpec spec)
    {
        var d = profile.Dialect;
        var registry = new ProviderRegistry();
        registry.Register(provider);
        var manager = new SessionManager(registry, new ScenarioProfileStore(password), new ScenarioTunnelFactory());
        var session = await manager.OpenAsync(profile, Ct);
        try
        {
            var schema = d switch { DatabaseDialect.Sqlite => "main", DatabaseDialect.PostgreSql => "public", DatabaseDialect.SqlServer => "dbo", _ => profile.Database };
            var t = SqlQuoting.QualifiedName(schema, "qf_t", d);
            var rig = new Rig(session);

            await SeveralStatementsAndCursor(rig, t);
            await FailureStopsTheScript(rig, t);
            await NamedAndPositionalParameters(rig, t, spec);
            await CancelStopsTheRun(rig, spec);
            await HugeResultAndExport(rig, spec);
            await CompletionSeesTheNewTable(session, spec);
        }
        finally
        {
            await manager.CloseAsync(session);
        }
    }

    private static async Task SeveralStatementsAndCursor(Rig rig, string t)
    {
        rig.Editor.Text = $"create table {t} (id integer primary key, name varchar(50)); insert into {t} (id, name) values (1, 'a'), (2, 'b'), (3, 'c'); select id, name from {t} order by id";
        await rig.Tab.RunAllCommand.ExecuteAsync(null);
        Assert.Equal([ResultKind.Message, ResultKind.Message, ResultKind.Grid], rig.Tab.Results.Select(r => r.Kind));
        Assert.Contains("3", rig.Tab.Results[1].Text, StringComparison.Ordinal);
        Assert.Equal(3, rig.Tab.Results[2].Grid!.RowCount);
        Assert.Equal(3, rig.History.Count);
        Assert.All(rig.History, h => Assert.True(h.Succeeded));

        // Run (not Run All) executes only the statement under the caret.
        rig.Editor.Text = $"select 1 as one; select name from {t} where id = 2";
        rig.Editor.CaretOffset = rig.Editor.Text.Length - 3;
        await rig.Tab.RunCommand.ExecuteAsync(null);
        var only = Assert.Single(rig.Tab.Results);
        Assert.Equal("b", Convert.ToString(only.Grid!.GetCell(0, 0), CultureInfo.InvariantCulture));
    }

    private static async Task FailureStopsTheScript(Rig rig, string t)
    {
        rig.History.Clear();
        rig.Editor.Text = $"select 1 as one; select * from qf_missing; select count(*) from {t}";
        await rig.Tab.RunAllCommand.ExecuteAsync(null);
        Assert.Equal([ResultKind.Grid, ResultKind.Message], rig.Tab.Results.Select(r => r.Kind));
        Assert.Contains("qf_missing", rig.Tab.Results[1].Text, StringComparison.OrdinalIgnoreCase);
        Assert.Equal([true, false], rig.History.Select(h => h.Succeeded));
        Assert.False(rig.Tab.IsRunning);
    }

    private static async Task NamedAndPositionalParameters(Rig rig, string t, QueryFlowSpec spec)
    {
        rig.Dialogs.Answer = names => [.. names.Select(n => new ParameterValue(n, "c", false))];
        rig.Editor.Text = $"select id from {t} where name = {spec.Sigil}who";
        await rig.Tab.RunCommand.ExecuteAsync(null);
        Assert.Equal(["who"], rig.Dialogs.AskedNames);
        Assert.Equal(3, Convert.ToInt32(Assert.Single(rig.Tab.Results).Grid!.GetCell(0, 0), CultureInfo.InvariantCulture));

        if (spec.Positional is null)
            return;
        rig.Dialogs.AskedNames = [];
        rig.Editor.Text = $"select id from {t} where name = {spec.Positional}";
        await rig.Tab.RunCommand.ExecuteAsync(null);
        var message = Assert.Single(rig.Tab.Results);
        Assert.Equal(ResultKind.Message, message.Kind);
        Assert.Equal(UiText.Get("query.positionalUnsupported"), message.Text);
    }

    private static async Task CancelStopsTheRun(Rig rig, QueryFlowSpec spec)
    {
        rig.Editor.Text = spec.SlowSql;
        var run = rig.Tab.RunCommand.ExecuteAsync(null);
        for (var i = 0; i < 50 && !rig.Tab.IsRunning; i++)
            await Task.Delay(100, TestContext.Current.CancellationToken);
        Assert.True(rig.Tab.IsRunning);
        await Task.Delay(500, TestContext.Current.CancellationToken);
        rig.Tab.CancelCommand.Execute(null);
        await run.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        Assert.Equal(UiText.Get("query.cancelled"), rig.Tab.StatusText);
        Assert.False(rig.Tab.IsRunning);

        rig.Editor.Text = "select 1 as one"; // the tab and its connection stay usable
        await rig.Tab.RunCommand.ExecuteAsync(null);
        Assert.Equal(ResultKind.Grid, Assert.Single(rig.Tab.Results).Kind);
    }

    private static async Task HugeResultAndExport(Rig rig, QueryFlowSpec spec)
    {
        rig.Editor.Text = spec.HugeSql;
        await rig.Tab.RunCommand.ExecuteAsync(null);
        var result = Assert.Single(rig.Tab.Results);
        Assert.Equal(100_000, result.Grid!.RowCount);
        Assert.True(result.Grid.IsComplete);
        Assert.False(result.Grid.IsTruncated);

        var directory = Directory.CreateTempSubdirectory("squill-qf-");
        try
        {
            foreach (IResultExporter exporter in new IResultExporter[] { new CsvResultExporter(), new JsonResultExporter() })
            {
                var path = Path.Combine(directory.FullName, "out." + exporter.FileExtension);
                rig.Dialogs.SavePath = path;
                await result.ExportCommand.ExecuteAsync(exporter);
                Assert.Empty(rig.Dialogs.Errors);
                Assert.True(new FileInfo(path).Length > 100_000, path);
            }

            Assert.Equal(100_001, File.ReadLines(Path.Combine(directory.FullName, "out.csv")).Count()); // header + rows
        }
        finally
        {
            directory.Delete(true);
        }
    }

    private static async Task CompletionSeesTheNewTable(ConnectionSession session, QueryFlowSpec spec)
    {
        await session.RefreshSchemaAsync(Ct);
        var provider = new CompositeCompletionProvider(new KeywordCompletionProvider(), new SchemaCompletionProvider());
        var items = provider.GetCompletions(new SqlRequest(spec.CompletionText, spec.CompletionText.Length, session.Profile.Dialect, session.Schema));
        Assert.Contains(items, i => i.Label == spec.CompletionLabel);
    }

    private sealed class Rig
    {
        public Rig(ConnectionSession session)
        {
            Editor = new StubSqlEditor();
            Tab = new QueryTabViewModel(session, Editor, History, new ScenarioSettings(), new ImmediateDispatcher(), Dialogs);
        }

        public StubSqlEditor Editor { get; }

        public QueryTabViewModel Tab { get; }

        public Dialogs Dialogs { get; } = new();

        public Recorder History { get; } = new();
    }

    private sealed class Recorder : List<HistoryEntry>, IQueryHistoryStore
    {
        public ValueTask AppendAsync(HistoryEntry e, CancellationToken ct)
        {
            Add(e);
            return ValueTask.CompletedTask;
        }

        public ValueTask<IReadOnlyList<HistoryEntry>> ListAsync(int max, CancellationToken ct) => ValueTask.FromResult<IReadOnlyList<HistoryEntry>>([.. this]);

        public ValueTask ClearAsync(CancellationToken ct)
        {
            Clear();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class Dialogs : IDialogService
    {
        public List<(string Title, string Message)> Errors { get; } = [];

        public Func<IReadOnlyList<string>, IReadOnlyList<ParameterValue>?> Answer { get; set; } = _ => null;

        public IReadOnlyList<string> AskedNames { get; set; } = [];

        public string? SavePath { get; set; }

        public ValueTask<Squill.Presentation.Safety.ProductionConfirmationResult> ConfirmProductionAsync(Squill.Presentation.Safety.ProductionConfirmationRequest request) =>
            ValueTask.FromResult(new Squill.Presentation.Safety.ProductionConfirmationResult(true, false));

        public ValueTask<bool> ConfirmAsync(string title, string message) => ValueTask.FromResult(true);

        public ValueTask<string?> PromptAsync(string title, string message, string? initial) => ValueTask.FromResult(initial);

        public ValueTask<string?> PickSaveFileAsync(string suggestedName, string extension) => ValueTask.FromResult(SavePath);

        public ValueTask ShowErrorAsync(string title, string message)
        {
            Errors.Add((title, message));
            return ValueTask.CompletedTask;
        }

        public ValueTask<IReadOnlyList<ParameterValue>?> PromptParametersAsync(IReadOnlyList<string> names, IReadOnlyDictionary<string, ParameterValue> defaults)
        {
            AskedNames = names;
            return ValueTask.FromResult(Answer(names));
        }
    }
}
