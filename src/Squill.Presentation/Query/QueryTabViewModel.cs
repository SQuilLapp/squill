using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Squill.Data;
using Squill.Platform.Settings;
using Squill.Platform.Storage;
using Squill.Presentation.Editor;
using Squill.Presentation.Results;
using Squill.Presentation.Sessions;
using Squill.Sql.Analysis;
using Squill.Sql.Completion;

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
    private readonly Dictionary<string, ParameterValue> _lastValues = [];
    private Action? _cancel;
    private string _baselineText;

    /// <summary>Creates the tab and wires the editor with the dialect, completion, hover and diagnostics of the session.</summary>
    /// <param name="session">The session the statements run on.</param>
    /// <param name="editor">The editor holding the script.</param>
    /// <param name="history">Receives one entry per executed statement.</param>
    /// <param name="settings">Provides the grid row limit.</param>
    /// <param name="dispatcher">Used by the grids to raise change notifications on the UI thread.</param>
    /// <param name="dialogs">Used to report failures that are not statement errors.</param>
    public QueryTabViewModel(
        ConnectionSession session,
        ISqlEditor editor,
        IQueryHistoryStore history,
        ISettingsStore settings,
        IUiDispatcher dispatcher,
        IDialogService dialogs)
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
        _baselineText = editor.Text;

        editor.SetDialect(session.Profile.Dialect);
        editor.SetCompletionProvider(new SessionCompletionProvider(session));
        editor.SetHoverProvider(r => SqlHoverProvider.GetHover(r with { Schema = session.Schema }));
        editor.TextChanged += (_, _) => UpdateDiagnostics();
        UpdateDiagnostics();
    }

    /// <summary>Gets the session the statements run on.</summary>
    public ConnectionSession Session => _session;

    /// <summary>Gets the editor holding the script.</summary>
    public ISqlEditor Editor => _editor;

    /// <summary>Gets whether the script text changed since the tab was opened or last run.</summary>
    public bool IsModified => _editor.Text != _baselineText;

    /// <summary>Gets or sets the tab title.</summary>
    [ObservableProperty]
    public partial string Title { get; set; } = "Query";

    /// <summary>Gets whether a run is in progress.</summary>
    [ObservableProperty]
    public partial bool IsRunning { get; private set; }

    /// <summary>Gets the duration of the last run.</summary>
    [ObservableProperty]
    public partial TimeSpan Elapsed { get; private set; }

    /// <summary>Gets a one-line summary of the last run.</summary>
    [ObservableProperty]
    public partial string StatusText { get; private set; } = "Ready";

    /// <summary>Gets the results of the last run, in statement order.</summary>
    public ObservableCollection<ResultViewModel> Results { get; } = [];

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
    private void Cancel() => _cancel?.Invoke();

    private async Task ExecuteAsync(IReadOnlyList<string> statements)
    {
        if (IsRunning || statements.Count == 0)
            return;
        _baselineText = _editor.Text;
        IsRunning = true;
        Results.Clear();
        SelectedResult = null;
        StatusText = "Running...";
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
                ? $"Completed {completed} statement(s) in {clock.Elapsed.TotalSeconds:0.###} s"
                : $"Failed after {completed} statement(s)";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Cancelled";
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
            AddResult(ResultViewModel.ForMessage(NextTitle(), "Positional parameters are not supported, use :name", _dialogs));
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
            if (reader.Columns.Count == 0)
            {
                var affected = reader.RecordsAffected;
                await reader.DisposeAsync();
                rows = affected >= 0 ? affected : null;
                AddResult(ResultViewModel.ForMessage(NextTitle(), affected >= 0 ? $"{affected} row(s) affected" : "Command completed", _dialogs));
            }
            else
            {
                var grid = new StreamingGridDataSource(reader, maxRows, _dispatcher);
                AddResult(ResultViewModel.ForGrid(NextTitle(), grid, _dialogs));
                await grid.StartAsync(ct);
                rows = grid.RowCount;
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

    private async Task RefreshAfterSchemaChangeAsync()
    {
        try
        {
            await RefreshSchemaAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            await _dialogs.ShowErrorAsync("Schema refresh failed", ex.Message);
        }
    }

    private void AddResult(ResultViewModel result)
    {
        Results.Add(result);
        SelectedResult = result;
    }

    private string NextTitle() => $"Result {Results.Count + 1}";

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
        _editor.SetDiagnostics(SqlDiagnosticsProvider.GetDiagnostics(
            new SqlRequest(_editor.Text, _editor.CaretOffset, _session.Profile.Dialect, _session.Schema)));

    /// <summary>Completes keywords and schema objects against the schema of the tab's current session.</summary>
    private sealed class SessionCompletionProvider(ConnectionSession session) : ISqlCompletionProvider
    {
        private static readonly CompositeCompletionProvider Inner =
            new(new KeywordCompletionProvider(), new SchemaCompletionProvider());

        /// <inheritdoc />
        public IReadOnlyList<CompletionItem> GetCompletions(SqlRequest request) =>
            Inner.GetCompletions(request with { Schema = session.Schema });
    }
}
