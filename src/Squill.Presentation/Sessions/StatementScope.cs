using Squill.Core;
using Squill.Core.Transactions;
using Squill.Data;
using Squill.Presentation.Localization;
using Squill.Sql.Transactions;

namespace Squill.Presentation.Sessions;

/// <summary>
/// Groups the statements of one flow (grid commit, designer apply) so they succeed or fail together: its own
/// transaction when none is open, a savepoint inside the user's open transaction.
/// </summary>
internal sealed class StatementScope
{
    private readonly IDatabaseConnection _connection;
    private readonly DatabaseDialect _dialect;
    private readonly SessionTransaction? _transaction;
    private readonly string _tab;
    private readonly string? _savepoint;
    private readonly List<(string Sql, long Rows)> _observed = [];
    private bool _finished;

    internal StatementScope(IDatabaseConnection connection, DatabaseDialect dialect, SessionTransaction? transaction, string tab, string? savepoint)
    {
        _connection = connection;
        _dialect = dialect;
        _transaction = transaction;
        _tab = tab;
        _savepoint = savepoint;
    }

    /// <summary>Records a statement that ran; it reaches the pending count only when the scope completes.</summary>
    /// <param name="sql">The statement text.</param>
    /// <param name="rowsAffected">The rows affected, or <c>-1</c>.</param>
    public void Observe(string sql, long rowsAffected) => _observed.Add((sql, rowsAffected));

    /// <summary>Commits the own transaction or releases the savepoint, then reports the statements to the session transaction.</summary>
    /// <param name="ct">Cancels the commit.</param>
    public async ValueTask CompleteAsync(CancellationToken ct)
    {
        if (_savepoint is null)
        {
            await SqlRunner.CommitAsync(_connection, _dialect, ct);
        }
        else if (TransactionSql.ReleaseSavepoint(_dialect, _savepoint) is { } release)
        {
            await SqlRunner.ExecuteAsync(_connection, release, [], ct);
        }

        _finished = true;
        if (_transaction is null)
            return;
        foreach (var (sql, rows) in _observed)
            _transaction.ObserveSuccess(sql, StatementClassifier.Classify(sql, _dialect), rows, _tab);
    }

    /// <summary>Undoes the scope: rolls back its own transaction, or only to its savepoint; never throws.</summary>
    public async ValueTask FailAsync()
    {
        if (_finished)
            return;
        _finished = true;
        if (_savepoint is null)
        {
            await SqlRunner.RollbackAsync(_connection, _dialect, CancellationToken.None);
            return;
        }

        try
        {
            await SqlRunner.ExecuteAsync(_connection, TransactionSql.RollbackToSavepoint(_dialect, _savepoint), [], CancellationToken.None);
        }
        catch (Exception ex)
        {
            if (_transaction is not null)
                await _transaction.ObserveFailureAsync(ex, StatementClassifier.Classify("ROLLBACK", _dialect), CancellationToken.None);
        }
    }
}
