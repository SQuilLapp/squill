namespace Squill.Core.Transactions;

/// <summary>How a session handles transactions.</summary>
public enum TransactionMode
{
    /// <summary>Every statement commits by itself (default).</summary>
    AutoCommit,
    /// <summary>A transaction is opened before the first statement that writes and stays open until committed or rolled back.</summary>
    Manual,
}

/// <summary>The state of the transaction of a session.</summary>
public enum TransactionState
{
    /// <summary>No transaction is open.</summary>
    None,
    /// <summary>A transaction is open.</summary>
    Open,
    /// <summary>The transaction is aborted and accepts only a rollback (PostgreSQL after an error).</summary>
    Failed,
}

/// <summary>What the server says about the transaction of a connection.</summary>
/// <param name="InTransaction">Whether a transaction is open, or <see langword="null"/> when the engine cannot tell.</param>
/// <param name="Failed">The transaction is aborted or doomed and can only be rolled back.</param>
public readonly record struct TransactionProbeResult(bool? InTransaction, bool Failed)
{
    /// <summary>Gets the answer of an engine that cannot tell.</summary>
    public static TransactionProbeResult Unknown => new(null, false);
}
