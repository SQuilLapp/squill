using Squill.Core.Transactions;

namespace Squill.Data;

/// <summary>
/// Optional capability of a connection: reports the server-side transaction state. Implemented by the SQLite, PostgreSQL,
/// MySQL and SQL Server connections; a connection without it does not support manual transactions (Oracle bridge).
/// </summary>
public interface ITransactionProbe
{
    /// <summary>Asks the server whether a transaction is open. Cheap (one round trip at most) and never changes the transaction.</summary>
    /// <param name="cancellationToken">Cancels the probe.</param>
    /// <returns>The state; <see cref="TransactionProbeResult.Unknown"/> on any server or driver error.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled; no other exception escapes.</exception>
    ValueTask<TransactionProbeResult> ProbeAsync(CancellationToken cancellationToken);
}
