using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Squill.Core;
using Squill.Core.Design;
using Squill.Presentation.Localization;
using Squill.Presentation.Sessions;
using Squill.Sql.Ddl;

namespace Squill.Presentation.Designer;

/// <summary>Edits the draft of a new or existing table, validates it, generates the script and applies it to the database.</summary>
public sealed partial class TableDesignerViewModel : ViewModelBase
{
    private const string MySqlWarning = "MySQL does not roll back DDL: statements before the failing one were applied";

    private readonly ConnectionSession _session;
    private readonly IDialogService _dialogs;
    private readonly DatabaseDialect _dialect;
    private IReadOnlyList<string> _statements = [];
    private bool _loading;
    private CancellationTokenSource? _cts;

    /// <summary>Creates the designer on a new table or on an existing one.</summary>
    /// <param name="session">The session the table lives in.</param>
    /// <param name="existing">The table to edit; null designs a new table.</param>
    /// <param name="dialogs">Used to confirm warnings and report failures.</param>
    /// <param name="dispatcher">The UI dispatcher.</param>
    public TableDesignerViewModel(ConnectionSession session, TableInfo? existing, IDialogService dialogs, IUiDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(dispatcher);
        _session = session;
        _dialogs = dialogs;
        _dialect = session.Profile.Dialect;
        TypeSuggestions = DataTypeCatalog.For(_dialect);
        Original = existing is null ? null : TableDesign.FromTable(existing);
        Columns.CollectionChanged += OnCollectionChanged;
        Indexes.CollectionChanged += OnCollectionChanged;
        ForeignKeys.CollectionChanged += OnCollectionChanged;
        Load(Original ?? StarterDesign());
    }

    /// <summary>Gets the session the table lives in.</summary>
    public ConnectionSession Session => _session;

    /// <summary>Gets the design the table had when it was read, or null for a new table.</summary>
    public TableDesign? Original { get; private set; }

    /// <summary>Gets the tab title.</summary>
    public string Title => Original is null ? "New table" : $"Design {Original.Name}";

    /// <summary>Gets the environment of the connection the table lives in.</summary>
    public ConnectionEnvironment Environment => _session.Profile.Environment;

    /// <summary>Gets whether the connection is a production one.</summary>
    public bool IsProd => Environment == ConnectionEnvironment.Production;

    /// <summary>Gets whether the connection is a staging one.</summary>
    public bool IsStaging => Environment == ConnectionEnvironment.Staging;

    /// <summary>Gets whether the connection is a development one.</summary>
    public bool IsDev => Environment == ConnectionEnvironment.Development;

    /// <summary>Gets whether the connection is a local one.</summary>
    public bool IsLocal => Environment == ConnectionEnvironment.Local;

    /// <summary>Gets the caps label of the environment, or null when none is set.</summary>
    public string? EnvironmentLabel => Environment switch
    {
        ConnectionEnvironment.Production => UiText.Get("status.production"),
        ConnectionEnvironment.Staging => UiText.Get("status.staging"),
        ConnectionEnvironment.Development => UiText.Get("status.dev"),
        ConnectionEnvironment.Local => UiText.Get("status.local"),
        _ => null,
    };

    /// <summary>Gets the connection, database and schema line under the table name.</summary>
    [ObservableProperty]
    public partial string Subtitle { get; private set; } = "";

    /// <summary>Gets the number of differences between the draft and the table in the database; 0 for a new table.</summary>
    [ObservableProperty]
    public partial int DraftChangeCount { get; private set; }

    /// <summary>Gets the draft state line, empty when the draft equals the table or the table is new.</summary>
    [ObservableProperty]
    public partial string DraftText { get; private set; } = "";

    /// <summary>Gets the column whose details are shown under the columns table.</summary>
    [ObservableProperty]
    public partial ColumnRowViewModel? SelectedColumn { get; set; }

    /// <summary>Gets the title of the selected column card.</summary>
    [ObservableProperty]
    public partial string SelectedColumnTitle { get; private set; } = "";

    /// <summary>Gets the data type suggestions of the dialect.</summary>
    public IReadOnlyList<string> TypeSuggestions { get; }

    /// <summary>Gets the values of <see cref="ReferentialAction"/>.</summary>
    public IReadOnlyList<ReferentialAction> ReferentialActions { get; } = Enum.GetValues<ReferentialAction>();

    /// <summary>Gets the column rows.</summary>
    public ObservableCollection<ColumnRowViewModel> Columns { get; } = [];

    /// <summary>Gets the index rows.</summary>
    public ObservableCollection<IndexRowViewModel> Indexes { get; } = [];

    /// <summary>Gets the foreign key rows.</summary>
    public ObservableCollection<ForeignKeyRowViewModel> ForeignKeys { get; } = [];

    /// <summary>Gets or sets the schema of the table.</summary>
    [ObservableProperty]
    public partial string? Schema { get; set; }

    /// <summary>Gets or sets the table name.</summary>
    [ObservableProperty]
    public partial string Name { get; set; } = "";

    /// <summary>Gets the draft built from the current rows.</summary>
    [ObservableProperty]
    public partial TableDesign Design { get; private set; } = TableDesign.New(null, "");

    /// <summary>Gets the validation issues of the draft, errors first.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<DesignIssue> Issues { get; private set; } = [];

    /// <summary>Gets the generated script; empty while an error exists.</summary>
    [ObservableProperty]
    public partial string Script { get; private set; } = "";

    /// <summary>Gets whether the draft differs from the table in the database (always true for a new table).</summary>
    [ObservableProperty]
    public partial bool HasChanges { get; private set; }

    /// <summary>Gets whether the draft has at least one error.</summary>
    [ObservableProperty]
    public partial bool HasErrors { get; private set; }

    /// <summary>Gets whether the script is being applied.</summary>
    [ObservableProperty]
    public partial bool IsApplying { get; private set; }

    /// <summary>Gets a one-line summary of the last apply.</summary>
    [ObservableProperty]
    public partial string StatusText { get; private set; } = "Ready";

    /// <summary>Raised with the generated script when the user asks to open it in a query tab.</summary>
    public event Action<string>? ScriptRequested;

    internal static IReadOnlyList<string> SplitNames(string text) =>
        [.. text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)];

    partial void OnSchemaChanged(string? value) => Recompute();

    partial void OnNameChanged(string value) => Recompute();

    partial void OnIsApplyingChanged(bool value) => ApplyCommand.NotifyCanExecuteChanged();

    partial void OnSelectedColumnChanged(ColumnRowViewModel? value) => UpdateSelectedTitle();

    private void UpdateSelectedTitle() =>
        SelectedColumnTitle = SelectedColumn is { } c ? UiText.Format("designer.selectedColumn", c.Name) : "";

    [RelayCommand]
    private void AddColumn()
    {
        var row = Wire(new ColumnRowViewModel(new ColumnDesign(null, Unique("new_column", Columns.Select(c => c.Name)), TypeSuggestions[0], true, null, false), false));
        Columns.Add(row);
        SelectedColumn = row;
    }

    [RelayCommand]
    private void RemoveColumn(ColumnRowViewModel row)
    {
        var i = Columns.IndexOf(row);
        Columns.Remove(row);
        if (ReferenceEquals(SelectedColumn, row) || SelectedColumn is null)
            SelectedColumn = Columns.Count == 0 ? null : Columns[Math.Min(Math.Max(i, 0), Columns.Count - 1)];
    }

    [RelayCommand]
    private void MoveColumnUp(ColumnRowViewModel row)
    {
        var i = Columns.IndexOf(row);
        if (i > 0)
            Columns.Move(i, i - 1);
    }

    [RelayCommand]
    private void MoveColumnDown(ColumnRowViewModel row)
    {
        var i = Columns.IndexOf(row);
        if (i >= 0 && i < Columns.Count - 1)
            Columns.Move(i, i + 1);
    }

    [RelayCommand]
    private void AddIndex() =>
        Indexes.Add(Wire(new IndexRowViewModel(new IndexDesign(null, Unique("new_index", Indexes.Select(x => x.Name)), false, []))));

    [RelayCommand]
    private void RemoveIndex(IndexRowViewModel row) => Indexes.Remove(row);

    [RelayCommand]
    private void AddForeignKey() =>
        ForeignKeys.Add(Wire(new ForeignKeyRowViewModel(new ForeignKeyDesign(
            null, Unique("new_fk", ForeignKeys.Select(x => x.Name)), [], Schema, "", [], ReferentialAction.NoAction, ReferentialAction.NoAction))));

    [RelayCommand]
    private void RemoveForeignKey(ForeignKeyRowViewModel row) => ForeignKeys.Remove(row);

    [RelayCommand]
    private void Revert() => Load(Original ?? StarterDesign());

    [RelayCommand]
    private void OpenScript() => ScriptRequested?.Invoke(Script);

    [RelayCommand]
    private void Cancel() => _cts?.Cancel();

    [RelayCommand(CanExecute = nameof(CanApply))]
    private async Task ApplyAsync()
    {
        var warnings = Issues.Where(i => i.Severity != DesignIssueSeverity.Error).Select(i => i.Message).ToList();
        if (warnings.Count > 0 && !await _dialogs.ConfirmAsync("Apply changes", string.Join("\n", warnings)))
            return;

        IsApplying = true;
        StatusText = "Applying...";
        using var cts = new CancellationTokenSource();
        _cts = cts;
        var statements = _statements;
        var transactional = DdlGenerators.UsesTransactionalDdl(_dialect);
        var lead = 0;
        var tail = statements.Count;
        if (transactional)
        {
            while (lead < tail && IsPragma(statements[lead]))
                lead++;
            while (tail > lead && IsPragma(statements[tail - 1]))
                tail--;
        }

        var inTransaction = false;
        try
        {
            for (var i = 0; i < statements.Count; i++)
            {
                if (transactional && i == lead && lead < tail)
                {
                    await SqlRunner.BeginAsync(_session.Connection, _dialect, cts.Token);
                    inTransaction = true;
                }

                if (transactional && i == tail && inTransaction)
                {
                    await SqlRunner.CommitAsync(_session.Connection, _dialect, cts.Token);
                    inTransaction = false;
                }

                await RunAsync(statements[i], cts.Token);
            }

            if (inTransaction)
            {
                await SqlRunner.CommitAsync(_session.Connection, _dialect, cts.Token);
                inTransaction = false;
            }

            await _session.RefreshSchemaAsync(cts.Token);
            Reload();
            StatusText = $"Applied {statements.Count} statement(s)";
        }
        catch (Exception ex)
        {
            if (inTransaction)
                await SqlRunner.RollbackAsync(_session.Connection, _dialect, CancellationToken.None);
            if (transactional)
            {
                // Restores what a SQLite rebuild switched off; the violation check is skipped on purpose.
                foreach (var pragma in statements.Skip(tail).Where(s => !s.Contains("foreign_key_check", StringComparison.OrdinalIgnoreCase)))
                    await TryRunAsync(pragma);
            }

            var message = ex is OperationCanceledException ? "Cancelled" : ex.Message;
            if (!transactional)
            {
                message += $"{System.Environment.NewLine}{MySqlWarning}";
                try
                {
                    await _session.RefreshSchemaAsync(CancellationToken.None);
                }
                catch (Exception)
                {
                    // The apply failure is the message worth showing.
                }
            }

            StatusText = "Apply failed";
            await _dialogs.ShowErrorAsync("Apply failed", message);
        }
        finally
        {
            _cts = null;
            IsApplying = false;
        }
    }

    private bool CanApply() => HasChanges && !HasErrors && !IsApplying;

    private static bool IsPragma(string sql) => sql.AsSpan().TrimStart().StartsWith("PRAGMA", StringComparison.OrdinalIgnoreCase);

    private ValueTask<long> RunAsync(string sql, CancellationToken ct) =>
        SqlRunner.ExecuteAsync(_session.Connection, sql, [], ct);

    private async Task TryRunAsync(string sql)
    {
        try
        {
            await RunAsync(sql, CancellationToken.None);
        }
        catch (Exception)
        {
            // Best effort: the original failure is reported instead.
        }
    }

    private void Reload()
    {
        var table = _session.Schema.FindTable(Design.Schema, Design.Name);
        if (table is null)
            return;
        Original = TableDesign.FromTable(table);
        OnPropertyChanged(nameof(Title));
        Load(Original);
    }

    private TableDesign StarterDesign()
    {
        // The first catalog entry is a small integer on most engines, which is a poor default key.
        var type = TypeSuggestions.FirstOrDefault(t => t is "integer" or "int" or "INTEGER") ?? TypeSuggestions[0];
        return new TableDesign(
            _session.Schema.DefaultSchema, "new_table",
            [new ColumnDesign(null, "id", type, false, null, true)], ["id"], null, [], []);
    }

    private void Load(TableDesign design)
    {
        _loading = true;
        try
        {
            Schema = design.Schema;
            Name = design.Name;
            Columns.Clear();
            foreach (var c in design.Columns)
                Columns.Add(Wire(new ColumnRowViewModel(c, design.PrimaryKey.Contains(c.Name, StringComparer.OrdinalIgnoreCase))));
            Indexes.Clear();
            foreach (var i in design.Indexes)
                Indexes.Add(Wire(new IndexRowViewModel(i)));
            ForeignKeys.Clear();
            foreach (var f in design.ForeignKeys)
                ForeignKeys.Add(Wire(new ForeignKeyRowViewModel(f)));
        }
        finally
        {
            _loading = false;
        }

        SelectedColumn = Columns.FirstOrDefault();
        Recompute();
    }

    private T Wire<T>(T row)
    {
        switch (row)
        {
            case ColumnRowViewModel c:
                c.Edited += Recompute;
                break;
            case IndexRowViewModel i:
                i.Edited += Recompute;
                break;
            case ForeignKeyRowViewModel f:
                f.Edited += Recompute;
                break;
        }

        return row;
    }

    // The original design as the rows present it: a primary key column cannot be nullable, so an untouched key row is not a draft change.
    private TableDesign? Baseline => Original is null ? null : Original with
    {
        Columns = [.. Original.Columns.Select(c => Original.PrimaryKey.Contains(c.Name, StringComparer.OrdinalIgnoreCase) ? c with { IsNullable = false } : c)],
    };

    private void UpdateRowStates(TableDesign design)
    {
        foreach (var row in Columns)
        {
            var before = Baseline?.Columns.FirstOrDefault(c => c.OriginalName is not null && string.Equals(c.OriginalName, row.OriginalName, StringComparison.Ordinal));
            row.IsNew = Baseline is not null && row.OriginalName is null;
            row.IsModified = before is not null
                && (before != row.ToDesign() || Baseline!.PrimaryKey.Contains(before.Name, StringComparer.OrdinalIgnoreCase) != row.IsPrimaryKey);
            row.ForeignKeyTarget = design.ForeignKeys
                .Where(f => f.Columns.Contains(row.Name, StringComparer.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(f.ReferencedTable))
                .Select(f => f.ReferencedTable).FirstOrDefault();
        }
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Recompute();

    private static string Unique(string stem, IEnumerable<string> existing)
    {
        var used = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var name = stem;
        for (var n = 2; used.Contains(name); n++)
            name = $"{stem}_{n}";
        return name;
    }

    private TableDesign BuildDesign() => new(
        Schema,
        Name,
        [.. Columns.Select(c => c.ToDesign())],
        [.. Columns.Where(c => c.IsPrimaryKey).Select(c => c.Name)],
        Original?.PrimaryKeyName,
        [.. Indexes.Select(i => i.ToDesign())],
        [.. ForeignKeys.Select(f => f.ToDesign())]);

    private void Recompute()
    {
        if (_loading)
            return;
        var design = BuildDesign();
        Design = design;

        var issues = DesignValidator.Validate(design, Original, _dialect).ToList();
        foreach (var fk in design.ForeignKeys)
        {
            var self = string.Equals(fk.ReferencedTable, design.Name, StringComparison.OrdinalIgnoreCase)
                && (fk.ReferencedSchema is null || string.Equals(fk.ReferencedSchema, design.Schema, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(fk.ReferencedTable) && !self && _session.Schema.FindTable(fk.ReferencedSchema, fk.ReferencedTable) is null)
                issues.Add(new DesignIssue(DesignIssueSeverity.Error, $"The foreign key '{fk.Name}' references the missing table '{fk.ReferencedTable}'.", fk.Name, CoreMessageCodes.DesignForeignKeyTableMissing, [fk.Name, fk.ReferencedTable]));
        }

        if (_dialect == DatabaseDialect.Sqlite && Original is not null && _session.Schema.Tables.Any(t => t.Kind == TableKind.View))
            issues.Add(new DesignIssue(DesignIssueSeverity.Warning, "Views depending on this table are not recreated.", null, CoreMessageCodes.DesignViewsNotRecreated));

        Issues = [.. issues.Where(i => i.Severity == DesignIssueSeverity.Error), .. issues.Where(i => i.Severity != DesignIssueSeverity.Error)];
        HasErrors = issues.Any(i => i.Severity == DesignIssueSeverity.Error);
        HasChanges = Original is null || TableDiff.Compute(Original, design).Count > 0;
        _statements = HasErrors ? [] : Original is null
            ? DdlGenerators.For(_dialect).CreateTable(design)
            : DdlGenerators.For(_dialect).Alter(Original, design);
        Script = DdlGenerators.ToScript(_statements);
        UpdateRowStates(design);
        DraftChangeCount = Baseline is { } baseline ? TableDiff.Compute(baseline, design).Count : 0;
        DraftText = DraftChangeCount == 0 ? "" : UiText.FormatPlural("designer.draftChanges", DraftChangeCount);
        var connection = _session.Profile;
        Subtitle = string.Join(" · ", new[] { connection.Name, connection.Database ?? connection.FilePath, design.Schema }.Where(x => !string.IsNullOrEmpty(x)));
        UpdateSelectedTitle();
        ApplyCommand.NotifyCanExecuteChanged();
    }
}
