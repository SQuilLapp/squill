using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Squill.Core;
using Squill.Core.Plans;
using Squill.Data;
using Squill.Platform.Settings;
using Squill.Platform.Storage;
using Squill.Presentation.Editing;
using Squill.Presentation.Localization;
using Squill.Presentation.Editor;
using Squill.Presentation.Plans;
using Squill.Presentation.Results;
using Squill.Presentation.Sessions;
using Squill.Sql.Analysis;
using Squill.Sql.Completion;
using Squill.Sql.Functions;
using Squill.Sql.Editing;
using Squill.Sql.Formatting;
using Squill.Sql.Plans;

namespace Squill.Presentation.Query;

/// <summary>Runs the statements of an editor on a session and exposes the grids and messages they produce.</summary>
public sealed partial class QueryTabViewModel : ViewModelBase
{
    private static readonly string[] SchemaChangingKeywords = ["CREATE", "ALTER", "DROP"];

    private readonly ConnectionSession _session;
    private readonly ISqlEditor _editor;
    private readonly IQueryHistoryStore _history;
    private readonly ISettingsStore _settings;
    private readonly IUiDispatcher _dispatcher;
    private readonly IDialogService _dialogs;
    private readonly TimeProvider _time;
    private KeywordCase _keywordCase = KeywordCase.Upper;
    private DateTimeOffset _keywordCaseLoadedAt;
    private readonly Dictionary<string, ParameterValue> _lastValues = [];
    private Action? _cancel;
    private string _baselineText;
    private PlanTabViewModel? _planTab;

    /// <summary>Raised with a DML script that a result editor wants opened in a new query tab.</summary>
    public event Action<string>? ScriptRequested;

    internal void RequestScript(string script) => ScriptRequested?.Invoke(script);

    /// <summary>Raised with the plan tab of this query tab when a plan was produced; the same instance each time, its content replaced.</summary>
    public event Action<PlanTabViewModel>? PlanReady;

    /// <summary>Creates the tab and wires the editor with the dialect, completion, hover and diagnostics of the session.</summary>
    /// <param name="session">The session the statements run on.</param>
    /// <param name="editor">The editor holding the script.</param>
    /// <param name="history">Receives one entry per executed statement.</param>
    /// <param name="settings">Provides the grid row limit.</param>
    /// <param name="dispatcher">Used by the grids to raise change notifications on the UI thread.</param>
    /// <param name="dialogs">Used to report failures that are not statement errors.</param>
    /// <param name="time">Clock that dates the keyword-case refresh; defaults to <see cref="TimeProvider.System"/>.</param>
    public QueryTabViewModel(
        ConnectionSession session,
        ISqlEditor editor,
        IQueryHistoryStore history,
        ISettingsStore settings,
        IUiDispatcher dispatcher,
        IDialogService dialogs,
        TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(dialogs);
        _session = session;
        _editor = editor;
        _history = history;
        _settings = settings;
        _dispatcher = dispatcher;
        _dialogs = dialogs;
        _time = time ?? TimeProvider.System;
        _baselineText = editor.Text;

        _ = LoadKeywordCaseAsync();
        _keywordCaseLoadedAt = _time.GetUtcNow();
        editor.SetDialect(session.Profile.Dialect);
        editor.SetCompletionProvider(new SessionCompletionProvider(session, CurrentKeywordCase));
        editor.SetHoverProvider(r => SqlHoverProvider.GetHover(r with { Schema = session.Schema }) is { } h ? h with { Markdown = IssueText.Format(h.Code, h.Args, h.Markdown) } : null);
        editor.TextChanged += (_, _) =>
        {
            UpdateDiagnostics();
            RefreshPlanState();
        };
        editor.CaretChanged += (_, _) => RefreshPlanState();
        UpdateDiagnostics();
        RefreshPlanState();
    }

    /// <summary>Gets the session the statements run on.</summary>
    public ConnectionSession Session => _session;

    /// <summary>Gets the editor holding the script.</summary>
    public ISqlEditor Editor => _editor;

    /// <summary>Gets whether the script text changed since the tab was opened or last run.</summary>
    public bool IsModified => _editor.Text != _baselineText;

    /// <summary>Gets or sets the tab title.</summary>
    [ObservableProperty]
    public partial string Title { get; set; } = UiText.Get("tab.untitled");

    /// <summary>Gets whether a run is in progress.</summary>
    [ObservableProperty]
    public partial bool IsRunning { get; private set; }

    partial void OnIsRunningChanged(bool value) => RefreshPlanState();

    /// <summary>Gets whether <see cref="ExplainCommand"/> can produce a plan now: the engine reads plans, the statement under the caret is explainable and nothing is running.</summary>
    [ObservableProperty]
    public partial bool CanExplain { get; private set; }

    /// <summary>Gets whether <see cref="AnalyzeCommand"/> can produce a plan now (it executes the statement, so only read-only ones qualify).</summary>
    [ObservableProperty]
    public partial bool CanAnalyze { get; private set; }

    /// <summary>Gets the tooltip text saying why <see cref="CanExplain"/> is false, empty when the statement is fine.</summary>
    [ObservableProperty]
    public partial string ExplainDisabledReason { get; private set; } = "";

    /// <summary>Gets the tooltip text saying why <see cref="CanAnalyze"/> is false, empty when the statement is fine.</summary>
    [ObservableProperty]
    public partial string AnalyzeDisabledReason { get; private set; } = "";

    /// <summary>Gets the duration of the last run.</summary>
    [ObservableProperty]
    public partial TimeSpan Elapsed { get; private set; }

    /// <summary>Gets a one-line summary of the last run.</summary>
    [ObservableProperty]
    public partial string StatusText { get; private set; } = UiText.Get("query.ready");

    /// <summary>Gets the results of the last run, in statement order.</summary>
    public ObservableCollection<ResultViewModel> Results { get; } = [];

    /// <summary>Gets whether any result holds uncommitted grid edits.</summary>
    public bool HasPendingChanges => Results.Any(r => r.HasPendingChanges);

    /// <summary>Gets or sets the result currently shown.</summary>
    [ObservableProperty]
    public partial ResultViewModel? SelectedResult { get; set; }

    /// <summary>Reads the schema again and refreshes the diagnostics; completion reads the new schema on its next request.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the schema is replaced.</returns>
    public async Task RefreshSchemaAsync(CancellationToken ct)
    {
        await _session.RefreshSchemaAsync(ct);
        UpdateDiagnostics();
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task RunAsync() => ExecuteAsync(RunTarget.Resolve(_editor, _session.Profile.Dialect));

    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task RunAllAsync() => ExecuteAsync(RunTarget.RunAll(_editor, _session.Profile.Dialect));

    [RelayCommand]
    private async Task FormatDocumentAsync(CancellationToken ct)
    {
        var old = _editor.Text;
        var settings = await _settings.LoadAsync(ct);
        var formatted = SqlFormatter.Format(old, _session.Profile.Dialect, FormatOptions.Default with { KeywordCase = settings.FormatterKeywordCase });
        if (formatted == old)
            return;
        var caret = _editor.CaretOffset;
        _editor.Text = formatted;
        _editor.CaretOffset = (int)((long)caret * formatted.Length / Math.Max(old.Length, 1));
    }

    [RelayCommand]
    private void Cancel() => _cancel?.Invoke();

    /// <summary>Shows the estimated plan of the statement under the caret; the statement is not executed. A plan is not a run and writes no history entry.</summary>
    [RelayCommand]
    private Task ExplainAsync() => RunPlanAsync(PlanMode.Estimated);

    /// <summary>Shows the actual plan of the statement under the caret; the statement is executed (read-only statements only, in a rolled-back transaction where the engine allows). A plan is not a run and writes no history entry.</summary>
    [RelayCommand]
    private Task AnalyzeAsync() => RunPlanAsync(PlanMode.Actual);

    private string PlanStatement()
    {
        var statements = RunTarget.Resolve(_editor, _session.Profile.Dialect);
        return statements.Count > 0 ? statements[0] : "";
    }

    private void RefreshPlanState()
    {
        var support = _session.PlanSupport;
        var eligibility = PlanEligibility.Evaluate(PlanStatement(), _session.Profile.Dialect);
        ExplainDisabledReason = !support.CanExplain ? UiText.Get("plan.unavailable.engine") : eligibility.ExplainReasonCode is { } e ? UiText.Get(e) : "";
        AnalyzeDisabledReason = !support.CanExplain ? UiText.Get("plan.unavailable.engine")
            : !support.CanAnalyze ? UiText.Get(PlanMessageCodes.ReasonNoAnalyze)
            : eligibility.AnalyzeReasonCode is { } a ? UiText.Get(a) : "";
        CanExplain = !IsRunning && ExplainDisabledReason.Length == 0;
        CanAnalyze = !IsRunning && AnalyzeDisabledReason.Length == 0;
    }

    private async Task RunPlanAsync(PlanMode mode)
    {
        if (IsRunning)
            return;
        RefreshPlanState();
        var dialect = _session.Profile.Dialect;
        var original = PlanStatement();
        var reason = mode == PlanMode.Estimated ? ExplainDisabledReason : AnalyzeDisabledReason;
        if (reason.Length > 0)
        {
            StatusText = reason;
            return;
        }

        if (ParameterScanner.HasPositional(original, dialect))
        {
            StatusText = UiText.Get("query.positionalUnsupported");
            return;
        }

        if (mode == PlanMode.Actual
            && _session.Profile.Environment == ConnectionEnvironment.Production
            && !await _dialogs.ConfirmAsync(UiText.Get("plan.analyze.confirm.title"), UiText.Get("plan.analyze.confirm.message")))
            return;
        IsRunning = true;
        StatusText = UiText.Get("query.running");
        using var cts = new CancellationTokenSource();
        _cancel = cts.Cancel;
        try
        {
            var values = await PromptValuesAsync([original]);
            if (values is null)
                throw new OperationCanceledException();
            var sql = ParameterScanner.Rewrite(original, dialect);
            List<QueryParameter> parameters = [];
            foreach (var name in ParameterScanner.Scan(original, dialect))
                parameters.Add(new QueryParameter(name, values.TryGetValue(name, out var v) && !v.IsNull ? v.Text : null));
            var (plan, message) = await LoadPlanAsync(sql, parameters, mode, cts.Token);
            if (_planTab is null)
                _planTab = new PlanTabViewModel(this, plan, sql, parameters, mode, message);
            else
                _planTab.Update(plan, sql, parameters, mode, message);
            StatusText = plan.StatusLine;
            PlanReady?.Invoke(_planTab);
        }
        catch (OperationCanceledException)
        {
            StatusText = UiText.Get("query.cancelled");
        }
        catch (PlanUnavailableException ex)
        {
            StatusText = UnavailableText(ex);
        }
        catch (Exception ex)
        {
            AddResult(ResultViewModel.ForMessage(NextTitle(), ex.Message, _dialogs));
            StatusText = ex.Message;
        }
        finally
        {
            _cancel = null;
            IsRunning = false;
        }
    }

    internal static string UnavailableText(PlanUnavailableException ex) =>
        ex.ReasonCode switch
        {
            "engine" => UiText.Get("plan.unavailable.engine"),
            "permission" => UiText.Get("plan.unavailable.permission"),
            "version" => UiText.Get("plan.unavailable.version"),
            "parameters" => UiText.Get("plan.unavailable.parameters"),
            _ => ex.Message,
        };

    /// <summary>Reads a plan; an engine output that cannot be parsed becomes a plan holding only the raw text, shown in text mode.</summary>
    internal async Task<(PlanViewModel Plan, string Message)> LoadPlanAsync(string statement, IReadOnlyList<QueryParameter> parameters, PlanMode mode, CancellationToken ct)
    {
        try
        {
            return (PlanViewModel.Create(await _session.ExplainAsync(statement, parameters, mode, ct)), "");
        }
        catch (PlanParseException ex)
        {
            var root = new PlanNode(0, UiText.Get("plan.raw"), PlanNodeKind.Other, null, null, null, null, null, null, null, null, null, [], [], false, []);
            var raw = new QueryPlan(_session.Profile.Dialect, mode, statement, root, [], [], null, null, null, false, PlanSourceFormat.Text, ex.SourceText);
            var vm = PlanViewModel.Create(raw);
            vm.ViewMode = PlanViewMode.Text;
            return (vm, ex.Message);
        }
    }

    private async Task ExecuteAsync(IReadOnlyList<string> statements)
    {
        if (IsRunning || statements.Count == 0)
            return;
        if (HasPendingChanges && !await _dialogs.ConfirmAsync(UiText.Get("query.run.title"), UiText.Get("query.run.discardChanges")))
            return;
        _baselineText = _editor.Text;
        IsRunning = true;
        Results.Clear();
        SelectedResult = null;
        StatusText = UiText.Get("query.running");
        var clock = Stopwatch.StartNew();
        using var cts = new CancellationTokenSource();
        _cancel = cts.Cancel;
        try
        {
            var maxRows = (await _settings.LoadAsync(cts.Token)).MaxGridRows;
            var values = await PromptValuesAsync(statements);
            if (values is null)
                throw new OperationCanceledException();
            var completed = 0;
            foreach (var sql in statements)
            {
                if (!await RunStatementAsync(sql, maxRows, values, cts.Token))
                    break;
                completed++;
            }

            StatusText = completed == statements.Count
                ? UiText.FormatPlural("query.completed", completed, clock.Elapsed.TotalSeconds.ToString("0.###", UiText.Culture))
                : UiText.FormatPlural("query.failedAfter", completed);
        }
        catch (OperationCanceledException)
        {
            StatusText = UiText.Get("query.cancelled");
        }
        finally
        {
            Elapsed = clock.Elapsed;
            _cancel = null;
            IsRunning = false;
        }
    }

    private async Task<Dictionary<string, ParameterValue>?> PromptValuesAsync(IReadOnlyList<string> statements)
    {
        var dialect = _session.Profile.Dialect;
        List<string> names = [];
        foreach (var name in statements.SelectMany(s => ParameterScanner.Scan(s, dialect)))
        {
            if (!names.Contains(name))
                names.Add(name);
        }

        if (names.Count == 0)
            return [];
        var entered = await _dialogs.PromptParametersAsync(names, _lastValues);
        if (entered is null)
            return null;
        foreach (var value in entered)
            _lastValues[value.Name] = value;
        return entered.ToDictionary(v => v.Name);
    }

    private async Task<bool> RunStatementAsync(string original, int maxRows, Dictionary<string, ParameterValue> values, CancellationToken ct)
    {
        var dialect = _session.Profile.Dialect;
        if (ParameterScanner.HasPositional(original, dialect))
        {
            AddResult(ResultViewModel.ForMessage(NextTitle(), UiText.Get("query.positionalUnsupported"), _dialogs));
            return false;
        }

        var sql = ParameterScanner.Rewrite(original, dialect);
        List<QueryParameter> parameters = [];
        foreach (var name in ParameterScanner.Scan(original, dialect))
            parameters.Add(new QueryParameter(name, values.TryGetValue(name, out var v) && !v.IsNull ? v.Text : null));
        var clock = Stopwatch.StartNew();
        long? rows = null;
        var succeeded = false;
        try
        {
            var reader = await _session.Connection.ExecuteAsync(sql, parameters, ct);
            try
            {
                var title = NextTitle();
                List<(ResultViewModel Result, StreamingGridDataSource Grid)> sets = [];
                if (reader.Columns.Count == 0)
                {
                    await reader.ReadBatchAsync(1, ct); // drivers report the count only once the command is drained
                    var affected = reader.RecordsAffected;
                    rows = affected >= 0 ? affected : null;
                    AddResult(ResultViewModel.ForMessage(title, affected >= 0 ? UiText.FormatPlural("query.rowsAffected", (int)Math.Min(affected, int.MaxValue)) : UiText.Get("query.commandCompleted"), _dialogs));
                }
                else
                {
                    do
                    {
                        if (reader.Columns.Count == 0)
                            continue;
                        var grid = new StreamingGridDataSource(reader, maxRows, _dispatcher, disposeReader: false);
                        var result = ResultViewModel.ForGrid(title, grid, _dialogs);
                        AddResult(result);
                        await grid.StartAsync(ct);
                        sets.Add((result, grid));
                    }
                    while (await reader.NextResultAsync(ct));

                    rows = sets.Count > 0 ? sets[0].Grid.RowCount : null;
                    if (sets.Count == 1)
                        AttachEditor(sets[0].Result, sets[0].Grid, original);
                    for (var i = 0; i < sets.Count && sets.Count > 1; i++)
                    {
                        sets[i].Result.SetReadOnly(UiText.Get("query.notEditableSets"));
                        sets[i].Result.SetTitle(UiText.Format("results.setOf", title, i + 1, sets.Count));
                    }
                }

                if (reader.Messages.Count > 0)
                    AddResult(ResultViewModel.ForMessages(UiText.Get("results.messages"), string.Join(Environment.NewLine, reader.Messages), _dialogs));
            }
            finally
            {
                await reader.DisposeAsync();
            }

            succeeded = true;
            if (IsSchemaChange(original))
                await RefreshAfterSchemaChangeAsync();
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            AddResult(ResultViewModel.ForMessage(NextTitle(), ex.Message, _dialogs));
            return false;
        }
        finally
        {
            await _history.AppendAsync(new HistoryEntry(DateTimeOffset.UtcNow, _session.Profile.Id, original, succeeded, rows, clock.Elapsed), CancellationToken.None);
        }
    }

    private void AttachEditor(ResultViewModel result, StreamingGridDataSource grid, string original)
    {
        try
        {
            var verdict = EditableSourceAnalyzer.Analyze(original, _session.Profile.Dialect, _session.Schema, grid.Columns);
            if (verdict.Target is null)
            {
                result.SetReadOnly(verdict.Reason ?? UiText.Get("query.notEditable"));
                return;
            }

            var editable = new EditableGridDataSource(grid, verdict.Target);
            var editor = new ResultEditorViewModel(editable, verdict.Target, _session, _dialogs, () => IsRunning);
            editor.ScriptRequested += script => ScriptRequested?.Invoke(script);
            result.SetEditor(editor);
        }
        catch (Exception)
        {
            result.SetReadOnly(UiText.Get("query.analysisFailed"));
        }
    }

    private async Task RefreshAfterSchemaChangeAsync()
    {
        try
        {
            await RefreshSchemaAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            await _dialogs.ShowErrorAsync(UiText.Get("query.schemaRefreshFailed"), ex.Message);
        }
    }

    private void AddResult(ResultViewModel result)
    {
        Results.Add(result);
        SelectedResult = result;
    }

    private string NextTitle() => UiText.Format("results.result", Results.Count + 1);

    private static bool IsSchemaChange(string sql)
    {
        var text = sql.AsSpan().TrimStart();
        foreach (var keyword in SchemaChangingKeywords)
        {
            if (text.StartsWith(keyword, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private void UpdateDiagnostics() =>
        _editor.SetDiagnostics([.. SqlDiagnosticsProvider.GetDiagnostics(
            new SqlRequest(_editor.Text, _editor.CaretOffset, _session.Profile.Dialect, _session.Schema))
            .Select(d => d with { Message = IssueText.Format(d.MessageCode, d.Args, d.Message) })]);

    /// <summary>Returns the keyword case of the settings; a value older than five seconds is refreshed in the background, so a change shows up in the next popup.</summary>
    private KeywordCase CurrentKeywordCase()
    {
        if (_time.GetUtcNow() - _keywordCaseLoadedAt > TimeSpan.FromSeconds(5))
        {
            _keywordCaseLoadedAt = _time.GetUtcNow();
            _ = LoadKeywordCaseAsync();
        }

        return _keywordCase;
    }

    private async Task LoadKeywordCaseAsync()
    {
        try
        {
            _keywordCase = (await _settings.LoadAsync(CancellationToken.None)).FormatterKeywordCase;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // keeps the previous value (Upper by default)
        }
    }

    /// <summary>Completes keywords, functions and schema objects against the schema of the tab's current session.</summary>
    private sealed class SessionCompletionProvider(ConnectionSession session, Func<KeywordCase> keywordCase) : ISqlCompletionProvider
    {
        private static readonly CompositeCompletionProvider Inner =
            new(new KeywordCompletionProvider(), new SchemaCompletionProvider(), new FunctionCompletionProvider(), new SnippetCompletionProvider());

        /// <inheritdoc />
        public IReadOnlyList<CompletionItem> GetCompletions(SqlRequest request) =>
            Inner.GetCompletions(request with { Schema = session.Schema, KeywordCase = keywordCase() });
    }
}
