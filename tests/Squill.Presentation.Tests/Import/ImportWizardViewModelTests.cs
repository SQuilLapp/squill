using System.Text;
using Squill.Core;
using Squill.Data;
using Squill.Presentation.Import;
using Squill.Presentation.Localization;
using Squill.Presentation.Safety;
using Squill.Presentation.Sessions;
using Xunit;

namespace Squill.Presentation.Tests.Import;

public sealed class ImportWizardViewModelTests : IDisposable
{
    private static readonly ColumnDefinition[] Columns = [new("id", "bigint", false, true, null), new("name", "text", true, false, null), new("amount", "numeric", true, false, null)];
    private static readonly TableInfo Items = new("public", "items", TableKind.Table, Columns, [], []);
    private static readonly TableInfo Report = new("public", "report", TableKind.View, Columns, [], []);
    private readonly string _dir = Directory.CreateTempSubdirectory("squill-wizard-").FullName;
    private readonly FakeDialogService _dialogs = new();

    // The wizard may still hold items.csv for a moment on Windows; the temp folder is not worth a failed test.
    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, true);
        }
        catch (IOException)
        {
        }
    }

    private sealed class Reader : IResultReader
    {
        public IReadOnlyList<ColumnInfo> Columns => [];
        public long RecordsAffected => 1;
        public ValueTask<object?[][]?> ReadBatchAsync(int maxRows, CancellationToken cancellationToken) => ValueTask.FromResult<object?[][]?>(null);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class Conn : IDatabaseConnection
    {
        public List<string> Sql { get; } = [];
        public Action? OnInsert { get; set; }
        public string ServerVersion => "1";
        public int Inserts => Sql.Count(s => s.StartsWith("INSERT", StringComparison.Ordinal));

        public ValueTask<IResultReader> ExecuteAsync(string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken)
        {
            Sql.Add(sql);
            if (sql.StartsWith("INSERT", StringComparison.Ordinal))
                OnInsert?.Invoke();
            return ValueTask.FromResult<IResultReader>(new Reader());
        }

        public ValueTask<IReadOnlyList<string>> ListDatabasesAsync(CancellationToken cancellationToken) => ValueTask.FromResult<IReadOnlyList<string>>([]);
        public ValueTask<SchemaSnapshot> ReadSchemaAsync(CancellationToken cancellationToken) => ValueTask.FromResult(SchemaSnapshot.Empty);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static ConnectionSession Session(Conn c, ConnectionEnvironment env = ConnectionEnvironment.None) =>
        new(new ConnectionProfile(Guid.NewGuid(), "dev", DatabaseDialect.PostgreSql, "h", 1, "db", "u", null, null, env), c, new SchemaSnapshot("public", [Items, Report]), null);

    private string Csv(string name, params string[] lines)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllLines(path, lines, new UTF8Encoding(false));
        return path;
    }

    private string Items3(int rows = 3) => Csv("items.csv", ["id,name,amount", .. Enumerable.Range(1, rows).Select(i => $"{i},n{i},{i}.5")]);

    private ImportWizardViewModel Wizard(Conn conn, ConnectionEnvironment env = ConnectionEnvironment.None, TableInfo? table = null) =>
        new([Session(conn, env)], _dialogs, table: table);

    private static async Task<ImportWizardViewModel> ToMappingAsync(ImportWizardViewModel w, string path)
    {
        await w.SetFileAsync(path);
        await w.NextAsync();
        w.SelectedTable = "public.items";
        await w.NextAsync();
        return w;
    }

    [Fact]
    public async Task Next_is_disabled_until_each_step_is_complete()
    {
        using var _ = UiText.Override(UiLanguage.English);
        var w = Wizard(new Conn());
        Assert.Equal(ImportStep.Source, w.Step);
        Assert.False(w.CanGoNext);
        Assert.False(w.CanGoBack);
        await w.SetFileAsync(Items3());
        Assert.True(w.CanGoNext);
        await w.NextAsync();
        Assert.Equal(ImportStep.Target, w.Step);
        Assert.False(w.CanGoNext);
        w.SelectedTable = "public.items";
        Assert.True(w.CanGoNext);
        await w.NextAsync();
        Assert.Equal(ImportStep.Mapping, w.Step);
        Assert.True(w.CanGoNext);
        w.Back();
        Assert.Equal(ImportStep.Target, w.Step);
        Assert.Equal([ImportStepState.Done, ImportStepState.Current, ImportStepState.Pending, ImportStepState.Pending, ImportStepState.Pending], w.Steps.Select(s => s.State));
    }

    [Fact]
    public async Task ChooseFile_uses_the_picker_and_applies_the_sniffed_options()
    {
        using var _ = UiText.Override(UiLanguage.English);
        _dialogs.OpenFileResult = Csv("fr.csv", "id;name;amount", "1;Éloïse;1,5", "2;Bob;2,5");
        var w = Wizard(new Conn());
        await w.ChooseFileAsync();
        Assert.Equal(';', w.Delimiter);
        Assert.Equal("UTF-8", w.EncodingName);
        Assert.Equal("3 columns detected", w.ColumnsText);
        Assert.Equal(2, w.Preview!.TotalRows);
        Assert.Equal(["id", "name", "amount"], w.Preview.ColumnNames);
    }

    [Fact]
    public async Task Changing_an_option_refreshes_the_preview()
    {
        using var _ = UiText.Override(UiLanguage.English);
        var w = Wizard(new Conn());
        await w.SetFileAsync(Items3());
        w.Delimiter = ';';
        await w.RefreshPreviewAsync();
        Assert.Equal(["id,name,amount"], w.Preview!.ColumnNames);
        w.Delimiter = ',';
        w.HasHeader = false;
        await w.RefreshPreviewAsync();
        Assert.Equal(["col1", "col2", "col3"], w.Preview!.ColumnNames);
        Assert.Equal(4, w.Preview.TotalRows);
    }

    [Fact]
    public async Task Target_lists_tables_only_and_preselects_the_explorer_table()
    {
        using var _ = UiText.Override(UiLanguage.English);
        var w = Wizard(new Conn(), table: Items);
        Assert.Equal(["public.items"], w.TableNames);
        Assert.Equal("public.items", w.SelectedTable);
        Assert.Equal(ConnectionEnvironment.None, w.Environment);
        await w.SetFileAsync(Items3());
        await w.NextAsync();
        Assert.True(w.CanGoNext);
    }

    [Fact]
    public async Task Mapping_is_automatic_then_editable()
    {
        using var _ = UiText.Override(UiLanguage.English);
        var w = await ToMappingAsync(Wizard(new Conn()), Items3());
        Assert.Equal(3, w.Rows.Count);
        Assert.All(w.Rows, r => Assert.True(r.Enabled));
        Assert.Equal("id · bigint", w.Rows[0].Target);
        Assert.Equal("Text -> bigint", w.Rows[0].Conversion);
        Assert.Equal(["1", "2", "3"], w.Rows[0].Preview);
        Assert.Equal("3 columns mapped · 0 ignored · preview on 3 rows", w.MappingSummary);

        w.Rows[2].Target = null;
        Assert.False(w.Rows[2].Enabled);
        Assert.Equal("ignored", w.Rows[2].Status);
        Assert.Equal("2 columns mapped · 1 ignored · preview on 3 rows", w.MappingSummary);

        w.Rows[0].Enabled = false; // the required id column is now unfed
        Assert.False(w.CanGoNext);
        Assert.Contains(w.Issues, i => i.Contains("id", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Mapping_counts_the_columns_whose_values_do_not_convert()
    {
        using var _ = UiText.Override(UiLanguage.English);
        var path = Csv("bad.csv", "id,name,amount", "1,a,1.5", "x,b,2.5", "3,c,oops");
        var w = await ToMappingAsync(Wizard(new Conn()), path);
        Assert.Equal(2, w.WarningCount);
        Assert.True(w.Rows[0].IsWarning);
        Assert.Contains("x", w.Rows[0].Status, StringComparison.Ordinal);
        Assert.False(w.Rows[1].IsWarning);
        Assert.Contains("2 columns", w.WarningText, StringComparison.Ordinal);

        w.DecimalSeparator = ',';
        Assert.Equal("Decimal \",\" -> numeric", w.Rows[2].Conversion);
        w.TimeZoneId = "Nowhere/Land";
        Assert.False(w.CanGoNext);
    }

    [Fact]
    public async Task Run_reports_progress_and_a_summary()
    {
        using var _ = UiText.Override(UiLanguage.English);
        var conn = new Conn();
        var w = Wizard(conn);
        await ToMappingAsync(w, Items3(2500));
        await w.NextAsync(); // Options
        Assert.Equal(ImportStep.Options, w.Step);
        await w.NextAsync(); // Run: starts and waits
        Assert.Equal(ImportStep.Run, w.Step);
        Assert.False(w.IsRunning);
        Assert.Equal(ImportOutcome.Completed, w.Result!.Completed);
        Assert.Equal((2500, 2500, 0), (w.Read, w.Inserted, w.Rejected));
        Assert.Equal(3, conn.Inserts);
        Assert.Contains("2500 rows inserted", w.Summary, StringComparison.Ordinal);
        Assert.False(w.HasRejects);
        Assert.False(w.CanGoNext);
    }

    [Fact]
    public async Task Cancel_during_the_run_stops_it_and_keeps_the_committed_batches()
    {
        using var _ = UiText.Override(UiLanguage.English);
        var conn = new Conn();
        var w = Wizard(conn);
        conn.OnInsert = () => w.CancelCommand.Execute(null);
        await ToMappingAsync(w, Items3(2500));
        await w.NextAsync();
        await w.NextAsync();
        Assert.Equal(ImportOutcome.Cancelled, w.Result!.Completed);
        Assert.True(w.Inserted < 2500);
        Assert.Contains("cancelled", w.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Cancel_when_idle_closes_the_wizard()
    {
        var w = Wizard(new Conn());
        ImportWizardViewModel? closed = null;
        w.CloseRequested += x => closed = x;
        w.Cancel();
        Assert.Same(w, closed);
    }

    [Fact]
    public async Task Production_connection_asks_the_confirmation_and_a_refusal_cancels()
    {
        using var _ = UiText.Override(UiLanguage.English);
        _dialogs.ProductionResult = new ProductionConfirmationResult(false, false);
        var conn = new Conn();
        var w = Wizard(conn, ConnectionEnvironment.Production);
        Assert.Equal(ConnectionEnvironment.Production, w.Environment);
        await ToMappingAsync(w, Items3());
        await w.NextAsync();
        await w.NextAsync();
        Assert.Single(_dialogs.ProductionRequests);
        Assert.Equal(ImportOutcome.Cancelled, w.Result!.Completed);
        Assert.Equal(0, conn.Inserts);
    }

    [Fact]
    public async Task ViewTable_and_OpenRejects_raise_requests()
    {
        using var _ = UiText.Override(UiLanguage.English);
        var conn = new Conn();
        var w = Wizard(conn);
        await ToMappingAsync(w, Csv("rej.csv", "id,name,amount", "1,a,1", "x,b,2"));
        await w.NextAsync();
        await w.NextAsync();
        TableInfo? viewed = null;
        w.ViewTableRequested += (_, t) => viewed = t;
        w.ViewTableCommand.Execute(null);
        Assert.Equal("items", viewed!.Name);
        Assert.Equal((1, 1), (w.Inserted, w.Rejected)); // "x" is not a bigint
        Assert.True(w.HasRejects);
        string? rejects = null;
        w.OpenRejectsRequested += p => rejects = p;
        w.OpenRejectsCommand.Execute(null);
        Assert.Equal(Path.Combine(_dir, "rej.rejets.csv"), rejects);
    }

    [Fact]
    public void DisabledReason_requires_a_connection()
    {
        Assert.NotEmpty(ImportWizardViewModel.DisabledReason(null));
        Assert.Empty(ImportWizardViewModel.DisabledReason(Session(new Conn())));
    }
}
