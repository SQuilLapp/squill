namespace Squill.Data.Informix;

/// <summary>Runs the blocking calls of one statement and lets a cancellation token stop them: <c>SQLCancel</c> is the one ODBC call allowed from another thread.</summary>
internal sealed class CancelGate(IOdbcApi api, nint statement)
{
    private readonly object _lock = new();
    private bool _running;

    /// <summary>Runs <paramref name="call"/>; when <paramref name="cancellationToken"/> fires meanwhile, the driver is asked to cancel the statement and the call fails with a diagnostic.</summary>
    /// <typeparam name="T">The result of the call.</typeparam>
    /// <param name="call">The blocking ODBC work.</param>
    /// <param name="cancellationToken">The token to watch.</param>
    /// <returns>What <paramref name="call"/> returned.</returns>
    /// <exception cref="OperationCanceledException">The token was already cancelled; nothing ran.</exception>
    public T Run<T>(Func<T> call, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_lock)
            _running = true;
        using var registration = cancellationToken.UnsafeRegister(_ =>
        {
            lock (_lock) // never cancel a handle whose call has returned: it may already be freed
            {
                if (_running)
                    api.Cancel(statement);
            }
        }, null);
        try
        {
            return call();
        }
        finally
        {
            lock (_lock)
                _running = false;
        }
    }
}
