using Squill.Core;
using Squill.Core.Transactions;
using Squill.Data;
using Squill.Presentation.Localization;
using Squill.Sql.Transactions;

namespace Squill.Presentation.Sessions;

/// <summary>A statement that ran successfully inside the open transaction and is not committed yet.</summary>
/// <param name="Text">The statement text, cut at 500 characters.</param>
/// <param name="Tab">The title of the tab that ran it.</param>
/// <param name="At">When it ran.</param>
/// <param name="RowsAffected">The rows affected, or <c>-1</c> when the engine does not report it.</param>
public sealed record PendingStatement(string Text, string Tab, DateTimeOffset At, long RowsAffected);

/// <summary>
/// The transaction of a session: mode, state, pending statements and the resynchronisation with the server.
/// It never touches the history, dialogs or the editor; callers raise changes from the UI thread.
/// </summary>
public sealed class SessionTransaction : ViewModelBase, IAsyncDisposable
{
    private const int MaxTextLength = 500;
    private const int MaxJournal = 200;

    private readonly IDatabaseConnection _connection;
    private readonly DatabaseDialect _dialect;
    private readonly TimeProvider _time;
    private readonly List<PendingStatement> _journal = [];
    private TransactionMode _mode;
    private TransactionState _state;
    private int _pending;
    private int _operations;
    private bool _running;
    private DateTimeOffset? _openedAt;

    internal SessionTransaction(IDatabaseConnection connection, DatabaseDialect dialect, TimeProvider time)
    {
        _connection = connection;
        _dialect = dialect;
        _time = time;
    }

    /// <summary>Raised, with the reason text, when the server ended the transaction without the app asking.</summary>
    public event EventHandler<string>? ExternallyEnded;

    /// <summary>Gets whether the engine supports manual transactions (its connection can be probed).</summary>
    public bool IsSupported => _connection is ITransactionProbe;

    /// <summary>Gets the mode of the session.</summary>
    public TransactionMode Mode
    {
        get => _mode;
        private set
        {
            if (SetProperty(ref _mode, value))
                OnPropertyChanged(nameof(StatusText));
        }
    }

    /// <summary>Gets the state of the transaction.</summary>
    public TransactionState State
    {
        get => _state;
        private set
        {
            if (!SetProperty(ref _state, value))
                return;
            if (value == TransactionState.None)
                OpenedAt = null;
            RaiseDerived();
        }
    }

    /// <summary>Gets how many statements that are not read-only succeeded since the transaction opened.</summary>
    public int PendingCount
    {
        get => _pending;
        private set
        {
            if (SetProperty(ref _pending, value))
                OnPropertyChanged(nameof(StatusText));
        }
    }

    /// <summary>Gets when the transaction opened, or <see langword="null"/> when none is open.</summary>
    public DateTimeOffset? OpenedAt
    {
        get => _openedAt;
        private set
        {
            if (SetProperty(ref _openedAt, value))
                OnPropertyChanged(nameof(ElapsedText));
        }
    }

    /// <summary>Gets the most recent pending statements, at most 200, oldest first.</summary>
    public IReadOnlyList<PendingStatement> Journal => _journal;

    /// <summary>Gets whether an operation of another kind, a commit or a rollback is running on the session.</summary>
    public bool IsBusy => _operations > 0 || _running;

    /// <summary>Gets whether <see cref="CommitAsync"/> can run now.</summary>
    public bool CanCommit => State == TransactionState.Open && !IsBusy;

    /// <summary>Gets whether <see cref="RollbackAsync"/> can run now.</summary>
    public bool CanRollback => State != TransactionState.None && !IsBusy;

    /// <summary>Gets the time since the transaction opened as text, empty when none is open.</summary>
    public string ElapsedText => OpenedAt is { } at ? FormatDuration(_time.GetUtcNow() - at) : "";

    /// <summary>Gets the status-bar text: empty in auto-commit with nothing open.</summary>
    public string StatusText => State switch
    {
        TransactionState.Failed => UiText.Get("tx.status.failed"),
        TransactionState.Open => UiText.Format("tx.status.open", ElapsedText, UiText.FormatPlural("tx.instructions", PendingCount)),
        _ => Mode == TransactionMode.Manual ? UiText.Get("tx.status.manual") : "",
    };

    /// <summary>Raises the change of <see cref="ElapsedText"/> and <see cref="StatusText"/> so the duration stays current.</summary>
    public void RefreshElapsed()
    {
        OnPropertyChanged(nameof(ElapsedText));
        OnPropertyChanged(nameof(StatusText));
    }

    /// <summary>Switches the mode.</summary>
    /// <param name="mode">The new mode.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <exception cref="InvalidOperationException">The engine does not support manual transactions, or a transaction is open and <paramref name="mode"/> is auto-commit.</exception>
    public ValueTask SetModeAsync(TransactionMode mode, CancellationToken ct)
    {
        if (mode == TransactionMode.Manual && !IsSupported)
            return ValueTask.FromException(new InvalidOperationException(UiText.Get("tx.unsupported")));
        if (mode == TransactionMode.AutoCommit && State != TransactionState.None)
            return ValueTask.FromException(new InvalidOperationException(UiText.Get("tx.autocommit.refused")));
        Mode = mode;
        return ValueTask.CompletedTask;
    }

    /// <summary>In manual mode, opens the transaction right before the first statement that is not read-only.</summary>
    /// <param name="traits">The traits of the statement about to run.</param>
    /// <param name="ct">Cancels the begin statement.</param>
    public async ValueTask EnsureOpenAsync(StatementTraits traits, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(traits);
        if (Mode != TransactionMode.Manual || State != TransactionState.None || traits.ReadOnly || traits.Control != TransactionControl.None)
            return;
        if (TransactionSql.Begin(_dialect) is { } begin)
            await SqlRunner.ExecuteAsync(_connection, begin, [], ct);
        Opened();
    }

    /// <summary>Records the effect of a statement that succeeded.</summary>
    /// <param name="statement">The statement text.</param>
    /// <param name="traits">Its traits.</param>
    /// <param name="rowsAffected">The rows affected, or <c>-1</c>.</param>
    /// <param name="tab">The title of the tab that ran it.</param>
    public void ObserveSuccess(string statement, StatementTraits traits, long rowsAffected, string tab)
    {
        ArgumentNullException.ThrowIfNull(traits);
        if (traits.ImplicitCommit && State == TransactionState.Open)
            Close();

        switch (traits.Control)
        {
            case TransactionControl.Begin when State == TransactionState.None:
                Opened();
                break;
            case TransactionControl.Commit or TransactionControl.Rollback:
                Close();
                break;
            case TransactionControl.RollbackToSavepoint when State == TransactionState.Failed:
                State = TransactionState.Open;
                break;
            case TransactionControl.None when !traits.ReadOnly && State == TransactionState.Open:
                PendingCount++;
                _journal.Add(new PendingStatement(
                    statement.Length > MaxTextLength ? statement[..MaxTextLength] : statement, tab, _time.GetUtcNow(), rowsAffected));
                if (_journal.Count > MaxJournal)
                    _journal.RemoveAt(0);
                OnPropertyChanged(nameof(Journal));
                break;
        }
    }

    /// <summary>Records a statement that failed; the statement is never counted.</summary>
    /// <param name="error">The failure.</param>
    /// <param name="traits">The traits of the statement.</param>
    /// <param name="ct">Cancels the probe.</param>
    public async ValueTask ObserveFailureAsync(Exception error, StatementTraits traits, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(error);
        if (State != TransactionState.Open)
            return;
        if (_dialect == DatabaseDialect.PostgreSql)
            State = TransactionState.Failed;
        else
            await ResyncAsync(ct);
    }

    /// <summary>Aligns the state with what the server reports; an engine that cannot tell changes nothing.</summary>
    /// <param name="ct">Cancels the probe.</param>
    public async ValueTask ResyncAsync(CancellationToken ct)
    {
        if (_connection is not ITransactionProbe probe)
            return;
        var result = await probe.ProbeAsync(ct);
        switch (result.InTransaction)
        {
            case false when State != TransactionState.None:
                Close();
                ExternallyEnded?.Invoke(this, UiText.Get("tx.endedByServer"));
                break;
            case true when State == TransactionState.None:
                Opened();
                break;
        }

        if (result is { InTransaction: not false, Failed: true } && State == TransactionState.Open)
            State = TransactionState.Failed;
    }

    /// <summary>Commits the transaction.</summary>
    /// <param name="ct">Cancels the commit.</param>
    /// <exception cref="InvalidOperationException">No transaction is open, it has failed, or another operation runs.</exception>
    public async ValueTask CommitAsync(CancellationToken ct)
    {
        Begin(State == TransactionState.Open);
        try
        {
            await ResyncAsync(ct);
            if (State == TransactionState.Failed)
                throw new InvalidOperationException(UiText.Get("tx.failed"));
            if (State == TransactionState.None)
                return;
            await RunControlAsync(TransactionSql.Commit(_dialect), ct);
            Close();
        }
        finally
        {
            _running = false;
            RaiseDerived();
        }
    }

    /// <summary>Rolls the whole transaction back.</summary>
    /// <param name="ct">Cancels the rollback.</param>
    /// <exception cref="InvalidOperationException">No transaction is open, or another operation runs.</exception>
    public async ValueTask RollbackAsync(CancellationToken ct)
    {
        Begin(State != TransactionState.None);
        try
        {
            await RunControlAsync(TransactionSql.Rollback(_dialect), ct);
            Close();
        }
        finally
        {
            _running = false;
            RaiseDerived();
        }
    }

    /// <summary>Marks an operation of another kind (run, plan, grid commit) as running; dispose it to release the session.</summary>
    /// <returns>The scope to dispose.</returns>
    public IDisposable BeginOperation()
    {
        _operations++;
        RaiseDerived();
        return new Operation(this);
    }

    /// <summary>Rolls back an open transaction before the connection closes, ignoring every failure.</summary>
    public async ValueTask DisposeAsync()
    {
        if (State == TransactionState.None)
            return;
        try
        {
            await SqlRunner.ExecuteAsync(_connection, TransactionSql.Rollback(_dialect), [], CancellationToken.None);
        }
        catch (Exception)
        {
            // The connection is about to close and the server rolls back anyway.
        }
    }

    private async ValueTask RunControlAsync(string sql, CancellationToken ct)
    {
        try
        {
            await SqlRunner.ExecuteAsync(_connection, sql, [], ct);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            await ResyncAsync(ct);
            throw;
        }
    }

    private void Begin(bool preconditionHolds)
    {
        if (!preconditionHolds)
            throw new InvalidOperationException(UiText.Get(State == TransactionState.Failed ? "tx.failed" : "tx.none"));
        if (IsBusy)
            throw new InvalidOperationException(UiText.Get("tx.busy"));
        _running = true;
        RaiseDerived();
    }

    private void Opened()
    {
        _journal.Clear();
        PendingCount = 0;
        OpenedAt = _time.GetUtcNow();
        State = TransactionState.Open;
        OnPropertyChanged(nameof(Journal));
    }

    private void Close()
    {
        _journal.Clear();
        PendingCount = 0;
        State = TransactionState.None;
        OnPropertyChanged(nameof(Journal));
    }

    private void RaiseDerived()
    {
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(CanCommit));
        OnPropertyChanged(nameof(CanRollback));
        OnPropertyChanged(nameof(StatusText));
    }

    private static string FormatDuration(TimeSpan span)
    {
        var minutes = (int)Math.Max(0, span.TotalMinutes);
        return minutes switch
        {
            < 1 => UiText.Get("tx.duration.lessThanMinute"),
            < 60 => UiText.Format("tx.duration.minutes", minutes),
            _ => UiText.Format("tx.duration.hours", minutes / 60, (minutes % 60).ToString("00", UiText.Culture)),
        };
    }

    private sealed class Operation(SessionTransaction owner) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;
            owner._operations--;
            owner.RaiseDerived();
        }
    }
}
