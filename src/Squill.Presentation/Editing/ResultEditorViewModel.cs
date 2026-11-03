using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Squill.Core;
using Squill.Core.Editing;
using Squill.Presentation.Localization;
using Squill.Presentation.Results;
using Squill.Presentation.Sessions;
using Squill.Sql.Ddl;
using Squill.Sql.Editing;

namespace Squill.Presentation.Editing;

/// <summary>Headless state behind an editable result grid: edit operations, pending changes, validation, preview script, commit and discard.</summary>
public sealed partial class ResultEditorViewModel : ViewModelBase
{
    private static readonly ColumnInfo AnyColumn = new("", "", typeof(object), true, 0);

    private readonly ConnectionSession _session;
    private readonly IDialogService _dialogs;
    private readonly Func<bool> _isConnectionBusy;
    private readonly DatabaseDialect _dialect;
    private ChangeSet _changeSet = new([], []);
    private CancellationTokenSource? _cts;

    /// <summary>Creates the view-model over an editable grid.</summary>
    /// <param name="grid">The grid holding the pending changes.</param>
    /// <param name="target">The table the grid edits.</param>
    /// <param name="session">The session the commit runs on.</param>
    /// <param name="dialogs">Used to confirm destructive actions and report failures.</param>
    /// <param name="isConnectionBusy">Tells whether another query is running on the session's connection.</param>
    public ResultEditorViewModel(EditableGridDataSource grid, EditTarget target, ConnectionSession session, IDialogService dialogs, Func<bool> isConnectionBusy)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(isConnectionBusy);
        Grid = grid;
        Target = target;
        _session = session;
        _dialogs = dialogs;
        _isConnectionBusy = isConnectionBusy;
        _dialect = session.Profile.Dialect;
        grid.Changed += (_, _) => Refresh();
    }

    /// <summary>Raised with the DML script when the user asks to preview or open it in a query tab.</summary>
    public event Action<string>? ScriptRequested;

    /// <summary>Gets the grid holding the pending changes.</summary>
    public EditableGridDataSource Grid { get; }

    /// <summary>Gets the table the grid edits.</summary>
    public EditTarget Target { get; }

    /// <summary>Gets the validation issues of the pending changes, or the row a commit failed on.</summary>
    public ObservableCollection<EditIssue> Issues { get; } = [];

    /// <summary>Gets whether any row is inserted, modified or deleted and not yet committed.</summary>
    [ObservableProperty]
    public partial bool HasPendingChanges { get; private set; }

    /// <summary>Gets the number of rows with a pending change.</summary>
    [ObservableProperty]
    public partial int PendingCount { get; private set; }

    /// <summary>Gets a one-line summary of the pending changes or of the last commit.</summary>
    [ObservableProperty]
    public partial string StatusText { get; private set; } = "";

    /// <summary>Gets whether a commit is running.</summary>
    [ObservableProperty]
    public partial bool IsCommitting { get; private set; }

    partial void OnIsCommittingChanged(bool value) => NotifyCommands();

    /// <summary>Parses the typed text against the result column and records it.</summary>
    /// <param name="row">Grid row index.</param>
    /// <param name="column">Result column index.</param>
    /// <param name="text">The typed text; <see langword="null"/> is SQL NULL.</param>
    /// <returns>The reason the edit was refused, or <see langword="null"/> when it was recorded.</returns>
    public string? TrySetCell(long row, int column, string? text)
    {
        if (!Grid.CanEdit)
            return UiText.Get("results.rowsLoading");
        if (column < 0 || column >= Grid.Columns.Count || !Target.IsEditable(column))
            return UiText.Get("results.columnReadOnly");
        if (Grid.GetRowState(row) == RowState.Deleted)
            return UiText.Get("results.rowDeleted");
        if (!CellValueParser.TryParse(text, Grid.Columns[column], out var value, out var error))
            return error;
        Grid.SetCell(row, column, value);
        return null;
    }

    /// <summary>Sets a cell to SQL NULL, the only way to store NULL in a text column.</summary>
    /// <param name="row">Grid row index.</param>
    /// <param name="column">Result column index; must be editable.</param>
    public void SetNull(long row, int column) => Grid.SetCell(row, column, null);

    /// <summary>Appends a new row to insert.</summary>
    /// <returns>The index of the new row.</returns>
    public long AddRow() => Grid.AddRow();

    /// <summary>Marks rows for deletion.</summary>
    /// <param name="rows">Grid rows; duplicates are ignored.</param>
    public void DeleteRows(IReadOnlyList<long> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        foreach (var row in rows.Distinct().OrderDescending())
            Grid.DeleteRow(row);
    }

    /// <summary>Drops the pending changes of rows.</summary>
    /// <param name="rows">Grid rows; duplicates are ignored.</param>
    public void RevertRows(IReadOnlyList<long> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        foreach (var row in rows.Distinct().OrderDescending())
            Grid.RevertRow(row);
    }

    [RelayCommand]
    private void Cancel() => _cts?.Cancel();

    [RelayCommand(CanExecute = nameof(CanCommit))]
    private async Task CommitAsync()
    {
        if (!CanCommit())
            return;

        if (_isConnectionBusy())
        {
            await _dialogs.ShowErrorAsync(UiText.Get("results.commitBlocked.title"), UiText.Get("results.commitBlocked.message"));
            return;
        }

        var set = _changeSet;
        var deletes = set.Edits.Count(e => e.Edit is DeleteRow);
        if (deletes > 0)
        {
            var table = SqlQuoting.QualifiedName(Target.Schema, Target.Table, _dialect);
            if (!await _dialogs.ConfirmAsync(UiText.Get("results.deleteRows.title"), UiText.FormatPlural("results.deleteRows.confirm", deletes, table)))
                return;
        }

        IsCommitting = true;
        using var cts = new CancellationTokenSource();
        _cts = cts;
        try
        {
            var result = await new EditCommitter(_session.Connection, _dialect).CommitAsync(Target, Grid.Columns, set.Edits, cts.Token);
            Grid.AcceptChanges(result.GeneratedKeys);
            StatusText = UiText.FormatPlural("results.committed", result.StatementCount);
        }
        catch (OperationCanceledException)
        {
            StatusText = UiText.Get("query.cancelled");
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
            if (ex is EditCommitException { Row: { } row })
            {
                Issues.Add(new EditIssue(row, null, ex.Message));
                NotifyCommands();
            }

            await _dialogs.ShowErrorAsync(UiText.Get("results.commitFailed"), ex.Message);
        }
        finally
        {
            _cts = null;
            IsCommitting = false;
        }
    }

    [RelayCommand]
    private async Task DiscardAsync()
    {
        if (HasPendingChanges && !await _dialogs.ConfirmAsync(UiText.Get("results.discard.title"), UiText.FormatPlural("results.discard.confirm", PendingCount)))
            return;
        Grid.DiscardAll();
    }

    [RelayCommand(CanExecute = nameof(CanPreview))]
    private void PreviewScript()
    {
        if (!CanPreview())
            return;

        var blocks = _changeSet.Edits.Select(p =>
        {
            var s = DmlGenerator.Generate(p.Edit, Target, _dialect);
            var text = s.Sql + ";";
            return s.Parameters.Count == 0
                ? text
                : text + "\n-- " + string.Join(", ", s.Parameters.Select(x => $"@{x.Name} = {Describe(x.Value)}"));
        });
        ScriptRequested?.Invoke(string.Join("\n\n", blocks));
    }

    private static string Describe(object? value) => value switch
    {
        null => "NULL",
        string s => SqlQuoting.Literal(s),
        _ => CellFormatter.Format(value, AnyColumn) ?? "NULL",
    };

    private bool CanCommit() => HasPendingChanges && Issues.Count == 0 && !IsCommitting;

    private bool CanPreview() => HasPendingChanges && Issues.Count == 0;

    private void Refresh()
    {
        _changeSet = ChangeSetBuilder.Build(Grid, Target);
        Issues.Clear();
        foreach (var issue in _changeSet.Issues)
            Issues.Add(issue);
        PendingCount = Grid.PendingCount;
        HasPendingChanges = Grid.HasPendingChanges;
        StatusText = PendingCount > 0 ? UiText.FormatPlural("results.pendingChanges", PendingCount) : "";
        NotifyCommands();
    }

    private void NotifyCommands()
    {
        CommitCommand.NotifyCanExecuteChanged();
        PreviewScriptCommand.NotifyCanExecuteChanged();
    }
}
