using Microsoft.Data.Sqlite;
using SQLitePCL;

namespace Squill.Data.Sqlite;

/// <summary>Makes SQLite work cancellable: the driver only checks tokens between calls, so a running step is stopped with <c>sqlite3_interrupt</c>.</summary>
internal static class SqliteInterrupt
{
    /// <summary>Runs <paramref name="work"/> and interrupts the connection when the token is cancelled or the command timeout elapses.</summary>
    /// <param name="connection">Connection whose running statement is interrupted.</param>
    /// <param name="timeoutSeconds">Command timeout in seconds; 0 disables it. It applies to each call, not to a whole result.</param>
    /// <param name="work">The driver call to guard.</param>
    /// <param name="cancellationToken">Token of the caller.</param>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    /// <exception cref="TimeoutException">The command timeout elapsed.</exception>
    public static async ValueTask<T> RunAsync<T>(SqliteConnection connection, int timeoutSeconds, Func<ValueTask<T>> work, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var timeout = new CancellationTokenSource();
        if (timeoutSeconds > 0)
            timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        using var guard = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        using var registration = guard.Token.Register(static state => raw.sqlite3_interrupt(((SqliteConnection)state!).Handle), connection);
        try
        {
            return await work();
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == raw.SQLITE_INTERRUPT && guard.IsCancellationRequested)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(ex.Message, ex, cancellationToken);
            throw new TimeoutException(ex.Message, ex);
        }
    }
}
