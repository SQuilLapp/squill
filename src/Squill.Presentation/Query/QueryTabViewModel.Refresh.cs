using System.Diagnostics;
using Squill.Core.Transactions;
using Squill.Presentation.Localization;
using Squill.Presentation.Results;
using Squill.Sql.Analysis;
using Squill.Sql.Transactions;
using Squill.Presentation.Safety;

namespace Squill.Presentation.Query;

/// <summary>How the statements of a run are executed: as a user run, or as a refresh of the last run.</summary>
internal enum RunMode
{
    /// <summary>A run asked by the user: results replaced at once, history written.</summary>
    Run,

    /// <summary>A refresh of the last run: results staged then swapped in one step, no history entry.</summary>
    Refresh,
}

/// <summary>The result of <see cref="QueryTabViewModel.RefreshAsync"/>.</summary>
internal enum RefreshOutcome
{
    /// <summary>Every statement completed and the new results replaced the old ones.</summary>
    Refreshed,

    /// <summary>A statement failed; the error result replaced the old results.</summary>
    Failed,

    /// <summary>The refresh was cancelled; the old results are kept.</summary>
    Cancelled,

    /// <summary>Nothing ran and nothing changed: the tab was not in a state that allows a refresh.</summary>
    Skipped,
}

public sealed partial class QueryTabViewModel
{
    private sealed record LastRun(IReadOnlyList<string> Statements, Dictionary<string, ParameterValue> Values);

    private LastRun? _lastRun;
    private List<ResultViewModel>? _staged;

    /// <summary>Raised just before a refresh replaces the results; the heads save the grid scroll and column widths.</summary>
    public event Action? ResultsReplacing;

    /// <summary>Raised just after a refresh replaced the results; the heads restore the grid scroll and column widths.</summary>
    public event Action? ResultsReplaced;

    /// <summary>Gets whether the last run can be refreshed: it only read data, needed no production confirmation and completed.</summary>
    public bool CanRefresh => _lastRun is not null;

    /// <summary>Gets whether a refresh is in progress, from its start to the end of the swap of the results.</summary>
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial bool IsRefreshing { get; private set; }

    private void SetLastRun(LastRun? value)
    {
        _lastRun = value;
        OnPropertyChanged(nameof(CanRefresh));
        OnPropertyChanged(nameof(AutoRefreshToolTip));
    }

    private bool IsRefreshable(IReadOnlyList<string> statements)
    {
        var dialect = _session.Profile.Dialect;
        return statements.All(s => StatementClassifier.Classify(s, dialect).ReadOnly && !ParameterScanner.HasPositional(s, dialect))
            && !ProductionGuard.Evaluate(_session.Profile, statements).Required;
    }

    private async Task<int> RunStatementsAsync(IReadOnlyList<string> statements, int maxRows, Dictionary<string, ParameterValue> values, RunMode mode, CancellationToken ct)
    {
        var completed = 0;
        foreach (var sql in statements)
        {
            if (!await RunStatementAsync(sql, maxRows, values, mode, ct))
                break;
            completed++;
        }

        return completed;
    }

    /// <summary>Runs the statements of the last run again, without prompt or history entry, and swaps the new results in once they are ready, keeping the view of the old ones.</summary>
    /// <param name="ct">Cancels the refresh; the old results are then kept.</param>
    /// <returns>What happened.</returns>
    internal async Task<RefreshOutcome> RefreshAsync(CancellationToken ct)
    {
        if (_lastRun is not { } last
            || IsRunning
            || Transaction.IsBusy
            || Transaction.State == TransactionState.Failed
            || HasPendingChanges
            || IsParametersPanelOpen
            || Results.Any(r => r.EditController?.IsEditing == true || r.Preview?.IsDirty == true))
            return RefreshOutcome.Skipped;

        IsRunning = true;
        IsRefreshing = true;
        using var operation = Transaction.BeginOperation();
        _staged = [];
        StatusText = UiText.Get("query.running");
        var clock = Stopwatch.StartNew();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _cancel = cts.Cancel;
        try
        {
            var maxRows = (await _settings.LoadAsync(cts.Token)).MaxGridRows;
            var completed = await RunStatementsAsync(last.Statements, maxRows, last.Values, RunMode.Refresh, cts.Token);
            var staged = _staged;
            _staged = null;
            await SwapAsync(staged);
            var ok = completed == last.Statements.Count;
            StatusText = ok
                ? UiText.FormatPlural("query.completed", completed, clock.Elapsed.TotalSeconds.ToString("0.###", UiText.Culture))
                : UiText.FormatPlural("query.failedAfter", completed);
            return ok ? RefreshOutcome.Refreshed : RefreshOutcome.Failed;
        }
        catch (OperationCanceledException)
        {
            foreach (var result in _staged ?? [])
                result.Release();
            StatusText = UiText.Get("query.cancelled");
            return RefreshOutcome.Cancelled;
        }
        finally
        {
            _staged = null;
            Elapsed = clock.Elapsed;
            _cancel = null;
            IsRunning = false;
            IsRefreshing = false;
        }
    }

    // The staged results get the view of the old ones before they are shown, so the heads never see an unfiltered grid.
    private async Task SwapAsync(List<ResultViewModel> staged)
    {
        var old = Results.ToList();
        var index = SelectedResult is null ? -1 : old.IndexOf(SelectedResult);
        var keep = SameShape(old, staged);
        if (keep)
        {
            for (var i = 0; i < old.Count; i++)
            {
                if (old[i].View is not { } oldView || staged[i].View is not { } view)
                    continue;
                view.CopyFrom(oldView);
                await view.WhenIdle();
                var grid = staged[i].DisplayGrid!;
                staged[i].Selection!.CopyFrom(old[i].Selection!, grid.RowCount, grid.Columns.Count);
            }
        }

        ResultsReplacing?.Invoke();
        foreach (var result in old)
            result.Release();
        Results.Clear();
        foreach (var result in staged)
            Results.Add(result);
        SelectedResult = keep && index >= 0 ? Results[index] : Results.LastOrDefault();
        ResultsReplaced?.Invoke();
    }

    private static bool SameShape(List<ResultViewModel> old, List<ResultViewModel> staged)
    {
        if (old.Count != staged.Count)
            return false;
        for (var i = 0; i < old.Count; i++)
        {
            if (old[i].Kind != staged[i].Kind)
                return false;
            if (old[i].Grid is { } a && !a.Columns.Select(c => c.Name).SequenceEqual(staged[i].Grid!.Columns.Select(c => c.Name), StringComparer.Ordinal))
                return false;
        }

        return true;
    }
}
