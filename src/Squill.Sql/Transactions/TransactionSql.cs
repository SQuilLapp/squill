using System.Globalization;
using Squill.Core;

namespace Squill.Sql.Transactions;

/// <summary>The transaction and savepoint statements of each dialect.</summary>
public static class TransactionSql
{
    /// <summary>Gets the begin statement.</summary>
    /// <param name="dialect">The engine.</param>
    /// <returns>The statement, or <see langword="null"/> when the engine begins transactions implicitly (Oracle).</returns>
    public static string? Begin(DatabaseDialect dialect) => dialect switch
    {
        DatabaseDialect.MySql => "START TRANSACTION",
        DatabaseDialect.SqlServer => "BEGIN TRANSACTION",
        DatabaseDialect.Oracle => null,
        _ => "BEGIN",
    };

    /// <summary>Gets the commit statement.</summary>
    /// <param name="dialect">The engine.</param>
    /// <returns>The statement.</returns>
    public static string Commit(DatabaseDialect dialect) => dialect == DatabaseDialect.SqlServer ? "COMMIT TRANSACTION" : "COMMIT";

    /// <summary>Gets the statement that rolls the whole transaction back.</summary>
    /// <param name="dialect">The engine.</param>
    /// <returns>The statement; on SQL Server it rolls back every nested level.</returns>
    public static string Rollback(DatabaseDialect dialect) => dialect == DatabaseDialect.SqlServer ? "ROLLBACK TRANSACTION" : "ROLLBACK";

    /// <summary>Whether the engine supports savepoints in this app.</summary>
    /// <param name="dialect">The engine.</param>
    /// <returns><see langword="false"/> for Oracle, whose bridge keeps auto-commit.</returns>
    public static bool SupportsSavepoints(DatabaseDialect dialect) => dialect != DatabaseDialect.Oracle;

    /// <summary>Gets the statement that creates a savepoint.</summary>
    /// <param name="dialect">The engine.</param>
    /// <param name="name">A name matching <c>[A-Za-z_][A-Za-z0-9_]{0,40}</c>.</param>
    /// <returns>The statement.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is not a valid savepoint name.</exception>
    public static string Savepoint(DatabaseDialect dialect, string name) =>
        (dialect == DatabaseDialect.SqlServer ? "SAVE TRANSACTION " : "SAVEPOINT ") + Checked(name);

    /// <summary>Gets the statement that rolls back to a savepoint and keeps the transaction.</summary>
    /// <param name="dialect">The engine.</param>
    /// <param name="name">A valid savepoint name.</param>
    /// <returns>The statement.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is not a valid savepoint name.</exception>
    public static string RollbackToSavepoint(DatabaseDialect dialect, string name) =>
        (dialect == DatabaseDialect.SqlServer ? "ROLLBACK TRANSACTION " : "ROLLBACK TO SAVEPOINT ") + Checked(name);

    /// <summary>Gets the statement that releases a savepoint.</summary>
    /// <param name="dialect">The engine.</param>
    /// <param name="name">A valid savepoint name.</param>
    /// <returns>The statement, or <see langword="null"/> on SQL Server, which has none.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is not a valid savepoint name.</exception>
    public static string? ReleaseSavepoint(DatabaseDialect dialect, string name)
    {
        var checkedName = Checked(name);
        return dialect == DatabaseDialect.SqlServer ? null : "RELEASE SAVEPOINT " + checkedName;
    }

    /// <summary>Builds the savepoint name the app generates.</summary>
    /// <param name="n">A sequence number.</param>
    /// <returns><c>squill_sp_</c> followed by <paramref name="n"/>.</returns>
    public static string SavepointName(int n) => "squill_sp_" + n.ToString(CultureInfo.InvariantCulture);

    private static string Checked(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (name.Length is 0 or > 41 || !(char.IsAsciiLetter(name[0]) || name[0] == '_'))
        {
            throw new ArgumentException("Invalid savepoint name.", nameof(name));
        }

        foreach (var c in name)
        {
            if (!(char.IsAsciiLetterOrDigit(c) || c == '_'))
            {
                throw new ArgumentException("Invalid savepoint name.", nameof(name));
            }
        }

        return name;
    }
}
