using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Squill.Core;
using Squill.Presentation.Localization;
using Squill.Presentation.Sessions;

namespace Squill.Presentation.Import;

/// <summary>The five steps of the import wizard (mockup M10).</summary>
public enum ImportStep
{
    /// <summary>Choose the file and how it is read.</summary>
    Source,

    /// <summary>Choose the connection and the table.</summary>
    Target,

    /// <summary>Map the CSV columns to the table columns.</summary>
    Mapping,

    /// <summary>Batching, error and commit policy.</summary>
    Options,

    /// <summary>Run the import and show the outcome.</summary>
    Run,
}

/// <summary>Where a step stands in the step list: done (check mark), current (highlighted) or still to come (numbered).</summary>
public enum ImportStepState
{
    /// <summary>The step is behind the user.</summary>
    Done,

    /// <summary>The step is on screen.</summary>
    Current,

    /// <summary>The step has not been reached.</summary>
    Pending,
}

/// <summary>One entry of the wizard's step list.</summary>
/// <param name="Number">The step number, 1 to 5.</param>
/// <param name="Title">The localized step name.</param>
/// <param name="State">Done, current or pending.</param>
public sealed record ImportStepItem(int Number, string Title, ImportStepState State);

/// <summary>
/// The CSV import wizard: file, target table, column mapping, options, execution. It only reads the file and checks the mapping
/// until the last step, where <see cref="ImportRunner"/> writes the rows (and asks the production confirmation itself).
/// </summary>
public sealed partial class ImportWizardViewModel : ViewModelBase, IDisposable
{
    private const int PreviewValues = 3;
    private const long LargeFileRows = 100_000;
    private static readonly Dictionary<string, Func<Encoding>> Encodings = new()
    {
        ["UTF-8"] = () => new UTF8Encoding(false),
        ["UTF-16"] = () => new UnicodeEncoding(false, false),
        ["Latin-1"] = () => Encoding.Latin1,
    };

    private readonly IDialogService _dialogs;
    private readonly IUiDispatcher _dispatcher;
    private readonly TimeProvider _time;
    private readonly List<(string Display, TableInfo Table)> _tables = [];
    private CancellationTokenSource? _previewCts;
    private CancellationTokenSource? _runCts;
    private bool _applying;
    private bool _building;

    /// <summary>Creates the wizard.</summary>
    /// <param name="sessions">The connected sessions the file can be imported into.</param>
    /// <param name="dialogs">File picker, confirmations and error messages.</param>
    /// <param name="session">The session to pre-select, or <see langword="null"/> for the first one.</param>
    /// <param name="table">The table to pre-select (the explorer row the wizard was opened from), or <see langword="null"/>.</param>
    /// <param name="dispatcher">Receives the progress reports; <see langword="null"/> runs them inline.</param>
    /// <param name="time">The clock of the runner, for tests.</param>
    public ImportWizardViewModel(
        IReadOnlyList<ConnectionSession> sessions,
        IDialogService dialogs,
        ConnectionSession? session = null,
        TableInfo? table = null,
        IUiDispatcher? dispatcher = null,
        TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(dialogs);
        Sessions = sessions;
        _dialogs = dialogs;
        _dispatcher = dispatcher ?? new ImmediateDispatcher();
        _time = time ?? TimeProvider.System;
        _applying = true;
        SelectedSession = session ?? (sessions.Count > 0 ? sessions[0] : null);
        _applying = false;
        LoadTables();
        if (table is not null)
            SelectedTable = _tables.FirstOrDefault(t => t.Table.Schema == table.Schema && t.Table.Name == table.Name).Display;
        UpdateSteps();
    }

    /// <summary>Raised when the wizard should close.</summary>
    public event Action<ImportWizardViewModel>? CloseRequested;

    /// <summary>Raised by <see cref="OpenRejectsCommand"/>; the head opens the file.</summary>
    public event Action<string>? OpenRejectsRequested;

    /// <summary>Raised by <see cref="ViewTableCommand"/>; the workspace opens the table in a query tab.</summary>
    public event Action<ConnectionSession, TableInfo>? ViewTableRequested;

    /// <summary>Cancels what is running and releases the wizard; the head calls it when the window closes.</summary>
    public void Dispose()
    {
        _runCts?.Cancel();
        _previewCts?.Cancel();
        _previewCts?.Dispose();
    }

    /// <summary>Tells whether the wizard can be opened, and says why not.</summary>
    /// <param name="session">The active session, or <see langword="null"/> when not connected.</param>
    /// <returns>An empty text when it can, else the localized reason (the tooltip of the disabled menu item).</returns>
    public static string DisabledReason(ConnectionSession? session) => session is null ? UiText.Get("import.disabled.notConnected") : "";


    /// <summary>Gets or sets the step on screen.</summary>
    [ObservableProperty]
    public partial ImportStep Step { get; private set; }

    /// <summary>Gets the step list with the state of each step.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<ImportStepItem> Steps { get; private set; } = [];

    /// <summary>Gets whether <see cref="NextCommand"/> can run: the current step is complete.</summary>
    public bool CanGoNext => !IsRunning && Step switch
    {
        ImportStep.Source => Preview is { ColumnNames.Count: > 0 },
        ImportStep.Target => SelectedSession is not null && SelectedTable is not null,
        ImportStep.Mapping => MappingIsValid,
        ImportStep.Options => BatchSize > 0,
        _ => false,
    };

    /// <summary>Gets whether <see cref="BackCommand"/> can run.</summary>
    public bool CanGoBack => Step != ImportStep.Source && !IsRunning;

    private void Refresh()
    {
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(CanGoBack));
        NextCommand.NotifyCanExecuteChanged();
        BackCommand.NotifyCanExecuteChanged();
    }

    private void UpdateSteps()
    {
        string[] names = [UiText.Get("import.step.source"), UiText.Get("import.step.target"), UiText.Get("import.step.mapping"), UiText.Get("import.step.options"), UiText.Get("import.step.run")];
        Steps = [.. names.Select((n, i) => new ImportStepItem(i + 1, n, i < (int)Step ? ImportStepState.Done : i == (int)Step ? ImportStepState.Current : ImportStepState.Pending))];
        Refresh();
    }

    /// <summary>Goes to the next step; from <see cref="ImportStep.Options"/> it starts the import and returns when it ended.</summary>
    /// <returns>A task that completes when the step change, or the whole import, is done.</returns>
    [RelayCommand(CanExecute = nameof(CanGoNext))]
    public async Task NextAsync()
    {
        switch (Step)
        {
            case ImportStep.Target:
                RebuildMapping();
                break;
            case ImportStep.Options:
                Step = ImportStep.Run;
                UpdateSteps();
                await RunAsync().ConfigureAwait(true);
                return;
        }

        Step++;
        UpdateSteps();
    }

    /// <summary>Goes back one step; leaving the run step forgets the previous outcome.</summary>
    [RelayCommand(CanExecute = nameof(CanGoBack))]
    public void Back()
    {
        if (Step == ImportStep.Run)
            Result = null;
        Step--;
        UpdateSteps();
    }

    /// <summary>Cancels the running import, or closes the wizard when nothing runs.</summary>
    [RelayCommand]
    public void Cancel()
    {
        if (IsRunning)
            _runCts?.Cancel();
        else
            CloseRequested?.Invoke(this);
    }

    // ---- source -----------------------------------------------------------------------------------------------------

    /// <summary>Gets or sets the CSV file.</summary>
    [ObservableProperty]
    public partial string FilePath { get; private set; } = "";

    /// <summary>Gets the encoding names the user can choose from.</summary>
    public IReadOnlyList<string> EncodingNames { get; } = [.. Encodings.Keys];

    /// <summary>Gets or sets the encoding name, one of <see cref="EncodingNames"/>.</summary>
    [ObservableProperty]
    public partial string EncodingName { get; set; } = "UTF-8";

    /// <summary>Gets or sets the field delimiter.</summary>
    [ObservableProperty]
    public partial char Delimiter { get; set; } = ',';

    /// <summary>Gets or sets the quote character.</summary>
    [ObservableProperty]
    public partial char Quote { get; set; } = '"';

    /// <summary>Gets or sets whether the first row holds the column names.</summary>
    [ObservableProperty]
    public partial bool HasHeader { get; set; } = true;

    /// <summary>Gets the first rows of the file, or <see langword="null"/> before a file was read.</summary>
    [ObservableProperty]
    public partial CsvPreview? Preview { get; private set; }

    /// <summary>Gets the <c>n colonnes détectées</c> text.</summary>
    [ObservableProperty]
    public partial string ColumnsText { get; private set; } = "";

    /// <summary>Gets the size and row count of the file; the row count lands when the file has been counted.</summary>
    [ObservableProperty]
    public partial string FileSummary { get; private set; } = "";

    partial void OnEncodingNameChanged(string value) => OnReadOptionChanged();

    partial void OnDelimiterChanged(char value) => OnReadOptionChanged();

    partial void OnQuoteChanged(char value) => OnReadOptionChanged();

    partial void OnHasHeaderChanged(bool value) => OnReadOptionChanged();

    private void OnReadOptionChanged()
    {
        if (!_applying && FilePath.Length > 0)
            _ = RefreshPreviewAsync();
    }

    private CsvOptions CsvOptions => new(Encodings.TryGetValue(EncodingName, out var make) ? make() : new UTF8Encoding(false), Delimiter, Quote, HasHeader);

    /// <summary>Asks for a file with the picker and reads it.</summary>
    /// <returns>A task that completes when the preview is shown.</returns>
    [RelayCommand]
    public async Task ChooseFileAsync()
    {
        if (await _dialogs.PickOpenFileAsync(UiText.Get("import.pick.title"), ["csv", "tsv", "txt"]).ConfigureAwait(true) is { } path)
            await SetFileAsync(path).ConfigureAwait(true);
    }

    /// <summary>Takes a file: guesses encoding and delimiter from its first bytes, then reads the preview.</summary>
    /// <param name="path">The file.</param>
    /// <returns>A task that completes when the preview and the row count are known.</returns>
    public async Task SetFileAsync(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        try
        {
            var head = new byte[8192];
            int read;
            await using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                read = await stream.ReadAsync(head, CancellationToken.None).ConfigureAwait(true);
            }

            var sniffed = CsvSniffer.Detect(head.AsSpan(0, read));
            _applying = true;
            FilePath = path;
            EncodingName = Encodings.First(e => e.Value().WebName == sniffed.Encoding.WebName).Key;
            Delimiter = sniffed.Delimiter;
            Quote = sniffed.Quote;
            HasHeader = sniffed.HasHeader;
            _applying = false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _applying = false;
            await _dialogs.ShowErrorAsync(UiText.Get("import.error.title"), ex.Message).ConfigureAwait(true);
            return;
        }

        await RefreshPreviewAsync().ConfigureAwait(true);
    }

    /// <summary>Reads the preview again with the current options; a newer call supersedes an older one.</summary>
    /// <returns>A task that completes when the preview and the row count are known.</returns>
    public async Task RefreshPreviewAsync()
    {
        if (FilePath.Length == 0)
            return;
        if (_previewCts is { } previousPreview)
            await previousPreview.CancelAsync();
        var cts = _previewCts = new CancellationTokenSource();
        var options = CsvOptions;
        try
        {
            var preview = await CsvPreview.ReadAsync(FilePath, options, cancellationToken: cts.Token).ConfigureAwait(true);
            Preview = preview;
            ColumnsText = UiText.Format("import.columnsDetected", preview.ColumnNames.Count);
            FileSummary = UiText.Format("import.fileSize", FormatBytes(preview.FileBytes));
            _mappingStale = true;
            Refresh();
            var total = await CsvPreview.CountRowsAsync(FilePath, options, cts.Token).ConfigureAwait(true);
            Preview = preview with { TotalRows = total };
            FileSummary = UiText.Format("import.fileSummary", FormatBytes(preview.FileBytes), total);
            OnPropertyChanged(nameof(LargeFileHint));
        }
        catch (OperationCanceledException)
        {
            // superseded by a newer refresh
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await _dialogs.ShowErrorAsync(UiText.Get("import.error.title"), ex.Message).ConfigureAwait(true);
        }
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1 << 20 => $"{bytes / (double)(1 << 20):0.#} MB",
        >= 1 << 10 => $"{bytes / (double)(1 << 10):0.#} KB",
        _ => $"{bytes} B",
    };

    // ---- target -----------------------------------------------------------------------------------------------------

    /// <summary>Gets the connected sessions the file can go into.</summary>
    public IReadOnlyList<ConnectionSession> Sessions { get; }

    /// <summary>Gets or sets the connection that receives the rows.</summary>
    [ObservableProperty]
    public partial ConnectionSession? SelectedSession { get; set; }

    /// <summary>Gets the tables (never views) of the selected session, as shown in the list.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> TableNames { get; private set; } = [];

    /// <summary>Gets or sets the target table, one of <see cref="TableNames"/>.</summary>
    [ObservableProperty]
    public partial string? SelectedTable { get; set; }

    /// <summary>Gets the environment of the selected connection, which the head turns into the pill.</summary>
    public ConnectionEnvironment Environment => SelectedSession?.Profile.Environment ?? ConnectionEnvironment.None;

    partial void OnSelectedSessionChanged(ConnectionSession? value)
    {
        if (_applying)
            return;
        LoadTables();
        OnPropertyChanged(nameof(Environment));
    }

    partial void OnSelectedTableChanged(string? value)
    {
        _mappingStale = true;
        Refresh();
    }

    private void LoadTables()
    {
        _tables.Clear();
        if (SelectedSession is { } session)
        {
            _tables.AddRange(session.Schema.Tables.Where(t => t.Kind == TableKind.Table)
                .Select(t => (t.Schema is null ? t.Name : t.Schema + "." + t.Name, t)));
        }

        TableNames = [.. _tables.Select(t => t.Display)];
        SelectedTable = null;
    }

    private TableInfo? TargetTable => _tables.FirstOrDefault(t => t.Display == SelectedTable).Table;

    // ---- mapping ----------------------------------------------------------------------------------------------------

    private bool _mappingStale = true;

    /// <summary>Gets the CSV columns with their target, conversion and status.</summary>
    public ObservableCollection<ImportMapRowViewModel> Rows { get; } = [];

    /// <summary>Gets the choices of a row's target: <c>name . type</c> of every table column.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> TargetChoices { get; private set; } = [];

    /// <summary>Gets or sets the decimal separator of the numbers in the file.</summary>
    [ObservableProperty]
    public partial char DecimalSeparator { get; set; } = '.';

    /// <summary>Gets or sets the exact date format such as <c>dd/MM/yyyy HH:mm</c>; empty reads ISO dates.</summary>
    [ObservableProperty]
    public partial string DateFormat { get; set; } = "";

    /// <summary>Gets or sets the IANA time zone the dates are written in; empty leaves them as written.</summary>
    [ObservableProperty]
    public partial string TimeZoneId { get; set; } = "";

    partial void OnDecimalSeparatorChanged(char value) => Revalidate();

    partial void OnDateFormatChanged(string value) => Revalidate();

    partial void OnTimeZoneIdChanged(string value) => Revalidate();

    /// <summary>Gets how field text is read, from the format options.</summary>
    public ImportFormat Format => new()
    {
        DecimalSeparator = DecimalSeparator,
        ThousandsSeparator = DecimalSeparator == ',' ? ' ' : null,
        DateFormat = string.IsNullOrWhiteSpace(DateFormat) ? null : DateFormat.Trim(),
        TimeZoneId = string.IsNullOrWhiteSpace(TimeZoneId) ? null : TimeZoneId.Trim(),
    };

    /// <summary>Gets the count of mapped columns whose sample values do not all convert.</summary>
    [ObservableProperty]
    public partial int WarningCount { get; private set; }

    /// <summary>Gets the warning band text, empty when there is nothing to warn about.</summary>
    [ObservableProperty]
    public partial string WarningText { get; private set; } = "";

    /// <summary>Gets the problems that block the import (required column unmapped, duplicate target, unknown time zone...).</summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> Issues { get; private set; } = [];

    /// <summary>Gets the footer, <c>7 colonnes mappées . 1 ignorée . aperçu sur 1 000 lignes</c>.</summary>
    [ObservableProperty]
    public partial string MappingSummary { get; private set; } = "";

    private bool MappingIsValid => Rows.Count > 0 && Issues.Count == 0 && Rows.Any(r => r.Enabled);

    private void RebuildMapping()
    {
        if (!_mappingStale || TargetTable is not { } table || Preview is not { } preview)
            return;
        _mappingStale = false;
        _building = true;
        Rows.Clear();
        TargetChoices = [.. table.Columns.Select(Choice)];
        var map = ImportMapper.AutoMap(preview.ColumnNames, table.Columns);
        foreach (var column in map.Columns)
        {
            var values = preview.Sample.Take(PreviewValues).Select(r => column.SourceIndex < r.Length ? r[column.SourceIndex] ?? "" : "").ToList();
            Rows.Add(new ImportMapRowViewModel(column.SourceIndex, column.SourceName, values, Revalidate)
            {
                Target = column.Target is null ? null : Choice(column.Target),
                Enabled = column.Enabled,
            });
        }

        _building = false;
        Revalidate();
    }

    private static string Choice(ColumnDefinition c) => c.Name + " · " + c.DataType;

    private ImportMapping CurrentMapping(TableInfo table) => new([.. Rows.Select(r =>
        new ColumnMap(r.SourceIndex, r.SourceName, table.Columns.FirstOrDefault(c => Choice(c) == r.Target), r.Enabled))]);

    private void Revalidate()
    {
        if (_building || TargetTable is not { } table || Preview is not { } preview || Rows.Count == 0)
            return;
        var format = Format;
        List<string> issues = [];
        if (!ImportFormat.TryResolveTimeZone(format.TimeZoneId, out _))
            issues.Add(UiText.Format("import.issue.badTimeZone", format.TimeZoneId));
        var mapping = CurrentMapping(table);
        var validation = ImportValidator.Validate(mapping, table.Columns, preview.Sample, format, SelectedSession?.Profile.Dialect);
        issues.AddRange(validation.TableIssues.Select(i => i.Message));
        if (!mapping.Active.Any())
            issues.Add(UiText.Get("import.error.noColumns"));
        var warnings = 0;
        foreach (var row in Rows)
        {
            var map = mapping.Columns[Rows.IndexOf(row)];
            var report = validation.Columns.FirstOrDefault(c => c.SourceIndex == row.SourceIndex);
            row.Conversion = map.Target is null ? "" : Conversion(map.Target, format);
            row.IsWarning = map.Enabled && report is { Failed: > 0 };
            row.Status = map.Target is null || !map.Enabled ? UiText.Get("import.status.ignored")
                : row.IsWarning ? UiText.Format("import.status.warning", report!.FirstExample ?? "")
                : UiText.Get("import.status.ok");
            if (row.IsWarning)
                warnings++;
        }

        WarningCount = warnings;
        WarningText = warnings == 0 ? "" : UiText.Format("import.warnings", warnings);
        Issues = issues;
        var mapped = mapping.Active.Count();
        MappingSummary = UiText.Format("import.mappingSummary", mapped, Rows.Count - mapped, preview.Sample.Count);
        Refresh();
    }

    private static string Conversion(ColumnDefinition target, ImportFormat format)
    {
        var clr = ValueConverter.ClrTypeFor(target.DataType);
        if (clr is null)
            return UiText.Get("import.conv.unsupported");
        if (clr == typeof(string))
            return UiText.Get("import.conv.text");
        if (clr == typeof(decimal) && format.DecimalSeparator == ',')
            return UiText.Format("import.conv.decimal", target.DataType);
        if (format.DateFormat is { } pattern && (clr == typeof(DateTime) || clr == typeof(DateOnly) || clr == typeof(DateTimeOffset)))
            return format.TimeZoneId is { } zone ? pattern + ", " + zone : pattern;
        return UiText.Format("import.conv.to", target.DataType);
    }

    // ---- options ----------------------------------------------------------------------------------------------------

    /// <summary>Gets or sets the rows per statement.</summary>
    [ObservableProperty]
    public partial int BatchSize { get; set; } = 1000;

    /// <summary>Gets or sets what happens to a row that cannot be imported (the <c>Lignes en erreur</c> choice).</summary>
    [ObservableProperty]
    public partial ImportErrorPolicy OnError { get; set; } = ImportErrorPolicy.Reject;

    /// <summary>Gets or sets whether the whole file is one transaction (all or nothing).</summary>
    [ObservableProperty]
    public partial bool SingleTransaction { get; set; }

    /// <summary>Gets or sets a rejects file chosen by the user; empty uses <see cref="DefaultRejectsPath"/>.</summary>
    [ObservableProperty]
    public partial string RejectsPath { get; set; } = "";

    /// <summary>Gets the rejects file used when the user chose none: <c>file.rejets.csv</c> next to the source.</summary>
    public string DefaultRejectsPath => FilePath.Length == 0 ? "" : Path.ChangeExtension(FilePath, ".rejets.csv");

    /// <summary>Gets whether the file is big enough for the hint that a single transaction holds a long lock.</summary>
    public bool LargeFileHint => SingleTransaction && Preview?.TotalRows > LargeFileRows;

    partial void OnBatchSizeChanged(int value) => Refresh();

    partial void OnSingleTransactionChanged(bool value) => OnPropertyChanged(nameof(LargeFileHint));

    /// <summary>Asks where to write the rejected rows.</summary>
    /// <returns>A task that completes when the user chose or cancelled.</returns>
    [RelayCommand]
    public async Task ChooseRejectsAsync()
    {
        var name = Path.GetFileNameWithoutExtension(DefaultRejectsPath);
        if (await _dialogs.PickSaveFileAsync(name, "csv").ConfigureAwait(true) is { } path)
            RejectsPath = path;
    }

    // ---- run --------------------------------------------------------------------------------------------------------

    /// <summary>Gets whether the import is running.</summary>
    [ObservableProperty]
    public partial bool IsRunning { get; private set; }

    /// <summary>Gets the rows read so far.</summary>
    [ObservableProperty]
    public partial long Read { get; private set; }

    /// <summary>Gets the rows inserted so far.</summary>
    [ObservableProperty]
    public partial long Inserted { get; private set; }

    /// <summary>Gets the rows rejected so far.</summary>
    [ObservableProperty]
    public partial long Rejected { get; private set; }

    /// <summary>Gets the progress between 0 and 1, 0 while the row count is unknown.</summary>
    [ObservableProperty]
    public partial double Fraction { get; private set; }

    /// <summary>Gets the <c>n lignes/s</c> text.</summary>
    [ObservableProperty]
    public partial string RateText { get; private set; } = "";

    /// <summary>Gets the <c>reste hh:mm:ss</c> text.</summary>
    [ObservableProperty]
    public partial string RemainingText { get; private set; } = "";

    /// <summary>Gets the final figures, or <see langword="null"/> while running or before the run.</summary>
    [ObservableProperty]
    public partial ImportResult? Result { get; private set; }

    /// <summary>Gets the summary line of the finished import.</summary>
    [ObservableProperty]
    public partial string Summary { get; private set; } = "";

    /// <summary>Gets whether a rejects file was written.</summary>
    public bool HasRejects => Result?.RejectsPath is not null;

    partial void OnResultChanged(ImportResult? value) => OnPropertyChanged(nameof(HasRejects));

    partial void OnIsRunningChanged(bool value) => Refresh();

    private async Task RunAsync()
    {
        if (SelectedSession is not { } session || TargetTable is not { } table || Preview is null)
            return;
        var options = new ImportOptions
        {
            BatchSize = BatchSize,
            OnError = OnError,
            CommitMode = SingleTransaction ? ImportCommitMode.SingleTransaction : ImportCommitMode.PerBatch,
            RejectsPath = RejectsPath.Length == 0 ? null : RejectsPath,
        };
        var job = new ImportJob(session, table, CurrentMapping(table), Format, CsvOptions, FilePath, options);
        Result = null;
        (Read, Inserted, Rejected, Fraction) = (0, 0, 0, 0);
        RateText = RemainingText = "";
        _runCts = new CancellationTokenSource();
        IsRunning = true;
        try
        {
            var result = await new ImportRunner(_dialogs, _time)
                .RunAsync(job, new Sink(p => _dispatcher.Post(() => Apply(p))), _runCts.Token).ConfigureAwait(true);
            (Read, Inserted, Rejected) = (result.Read, result.Inserted, result.Rejected);
            Summary = Describe(result);
            Result = result;
        }
        finally
        {
            IsRunning = false;
            _runCts.Dispose();
            _runCts = null;
        }
    }

    private void Apply(ImportProgress p)
    {
        (Read, Inserted, Rejected) = (p.Read, p.Inserted, p.Rejected);
        Fraction = Preview?.TotalRows is > 0 and var total ? Math.Min(1, p.Read / (double)total) : 0;
        RateText = UiText.Format("import.rate", (long)p.RowsPerSecond);
        RemainingText = p.Remaining is { } left ? UiText.Format("import.remaining", left.ToString(@"hh\:mm\:ss", System.Globalization.CultureInfo.InvariantCulture)) : "";
    }

    private static string Describe(ImportResult r) => r.Completed switch
    {
        ImportOutcome.Completed => UiText.Format("import.summary.completed", r.Inserted, r.Rejected, r.Elapsed.ToString(@"hh\:mm\:ss", System.Globalization.CultureInfo.InvariantCulture)),
        ImportOutcome.Cancelled => UiText.Format("import.summary.cancelled", r.Inserted),
        ImportOutcome.StoppedOnError => UiText.Format("import.summary.stopped", r.Inserted, r.Error ?? ""),
        _ => UiText.Format("import.summary.failed", r.Error ?? ""),
    };

    /// <summary>Asks the head to open the rejects file.</summary>
    [RelayCommand]
    public void OpenRejects()
    {
        if (Result?.RejectsPath is { } path)
            OpenRejectsRequested?.Invoke(path);
    }

    /// <summary>Asks the workspace to open the target table in a query tab.</summary>
    [RelayCommand]
    public void ViewTable()
    {
        if (SelectedSession is { } session && TargetTable is { } table)
            ViewTableRequested?.Invoke(session, table);
    }

    private sealed class Sink(Action<ImportProgress> report) : IProgress<ImportProgress>
    {
        public void Report(ImportProgress value) => report(value);
    }
}
