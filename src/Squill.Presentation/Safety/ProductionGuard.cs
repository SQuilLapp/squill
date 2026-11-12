using Squill.Core;
using Squill.Sql.Transactions;

namespace Squill.Presentation.Safety;

/// <summary>Whether a set of statements needs the production confirmation.</summary>
/// <param name="Required">The confirmation must be asked.</param>
/// <param name="RequiresTyping">The user must also type the database name.</param>
/// <param name="Traits">The traits of every statement, in order.</param>
public sealed record ProductionRequirement(bool Required, bool RequiresTyping, IReadOnlyList<StatementTraits> Traits);

/// <summary>Decides when a statement is held back for confirmation on a production connection.</summary>
public static class ProductionGuard
{
    /// <summary>Decides whether running <paramref name="statements"/> needs the production confirmation.</summary>
    /// <param name="profile">The connection the statements would run on.</param>
    /// <param name="statements">The statements, one text each.</param>
    /// <returns>The requirement; never required outside <see cref="ConnectionEnvironment.Production"/>.</returns>
    public static ProductionRequirement Evaluate(ConnectionProfile profile, IReadOnlyList<string> statements)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(statements);
        var traits = statements.Select(s => StatementClassifier.Classify(s, profile.Dialect)).ToArray();
        if (profile.Environment != ConnectionEnvironment.Production)
            return new ProductionRequirement(false, false, traits);
        var required = traits.Any(t => !t.ReadOnly || t.Control == TransactionControl.Commit || t.Kind is StatementKind.Call or StatementKind.Other);
        return new ProductionRequirement(required, required && traits.Any(t => t.Destructive), traits);
    }

    /// <summary>Gets the word the user types to confirm a destructive statement.</summary>
    /// <param name="profile">The connection.</param>
    /// <returns>The database name, the file name of a SQLite file, else the profile name.</returns>
    public static string RequiredText(ConnectionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.Dialect == DatabaseDialect.Sqlite && !string.IsNullOrWhiteSpace(profile.FilePath))
            return Path.GetFileName(profile.FilePath.TrimEnd('\\', '/'));
        return string.IsNullOrWhiteSpace(profile.Database) ? profile.Name : profile.Database;
    }

    /// <summary>Compares the typed text with the expected one.</summary>
    /// <param name="typed">What the user typed.</param>
    /// <param name="expected">The expected word.</param>
    /// <returns><see langword="true"/> when they are equal after trimming, ignoring case.</returns>
    public static bool Matches(string? typed, string expected) =>
        string.Equals(typed?.Trim(), expected.Trim(), StringComparison.OrdinalIgnoreCase);
}
