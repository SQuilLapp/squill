namespace Squill.Sql.Transactions;

/// <summary>The broad kind of a statement, decided from its first keywords.</summary>
public enum StatementKind
{
    /// <summary>No statement (blank text or comments only).</summary>
    Empty,
    /// <summary><c>SELECT</c>, <c>VALUES</c>, <c>TABLE</c> and <c>WITH</c> queries.</summary>
    Select,
    /// <summary><c>INSERT</c> and <c>REPLACE</c>.</summary>
    Insert,
    /// <summary><c>UPDATE</c>.</summary>
    Update,
    /// <summary><c>DELETE</c>.</summary>
    Delete,
    /// <summary><c>MERGE</c>.</summary>
    Merge,
    /// <summary><c>TRUNCATE</c>.</summary>
    Truncate,
    /// <summary><c>CREATE</c>.</summary>
    Create,
    /// <summary><c>ALTER</c>, <c>RENAME</c>, <c>COMMENT ON</c>, <c>GRANT</c> and <c>REVOKE</c>.</summary>
    Alter,
    /// <summary><c>DROP</c>.</summary>
    Drop,
    /// <summary>Transaction control: see <see cref="TransactionControl"/>.</summary>
    Transaction,
    /// <summary><c>CALL</c>, <c>EXEC</c>, <c>EXECUTE</c> and <c>DO</c>.</summary>
    Call,
    /// <summary>Session statements: <c>SET</c>, <c>PRAGMA</c>, <c>USE</c>, <c>SHOW</c>, <c>DESCRIBE</c>.</summary>
    Set,
    /// <summary>Anything not recognised.</summary>
    Other,
}

/// <summary>The transaction control a statement performs.</summary>
public enum TransactionControl
{
    /// <summary>None.</summary>
    None,
    /// <summary>Starts a transaction.</summary>
    Begin,
    /// <summary>Commits the transaction.</summary>
    Commit,
    /// <summary>Rolls the whole transaction back.</summary>
    Rollback,
    /// <summary>Creates a savepoint.</summary>
    Savepoint,
    /// <summary>Rolls back to a savepoint, keeping the transaction.</summary>
    RollbackToSavepoint,
    /// <summary>Releases a savepoint.</summary>
    ReleaseSavepoint,
}

/// <summary>What a single statement does to data, schema and the transaction.</summary>
/// <param name="Kind">The broad kind.</param>
/// <param name="ReadOnly">Nothing is written and no transaction is needed (false for unknown statements and calls).</param>
/// <param name="ModifiesData">Inserts, updates, deletes or truncates rows.</param>
/// <param name="ChangesSchema">Creates, alters, drops or truncates objects, or changes privileges.</param>
/// <param name="Destructive">Loses data that is hard to recover: no <c>WHERE</c>, <c>TRUNCATE</c>, <c>DROP</c>, <c>ALTER ... DROP</c>.</param>
/// <param name="NoWhere">An <c>UPDATE</c> or <c>DELETE</c> without a top-level <c>WHERE</c>.</param>
/// <param name="Control">The transaction control performed.</param>
/// <param name="ImplicitCommit">The engine commits the open transaction before running it (MySQL).</param>
/// <param name="CannotRunInTransaction">The engine refuses it, or aborts the transaction, inside a transaction.</param>
/// <param name="MayChangeTransaction">The transaction state can change in ways the text does not show.</param>
/// <param name="Target">The table written to, as written without quotes, or <see langword="null"/>.</param>
public sealed record StatementTraits(
    StatementKind Kind,
    bool ReadOnly,
    bool ModifiesData,
    bool ChangesSchema,
    bool Destructive,
    bool NoWhere,
    TransactionControl Control,
    bool ImplicitCommit,
    bool CannotRunInTransaction,
    bool MayChangeTransaction,
    string? Target);
