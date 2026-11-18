using Squill.Core;
using Squill.Presentation.Sessions;

namespace Squill.Presentation.Compare;

/// <summary>One side a schema comparison can read: a connected database, or another database of a connected server that is opened on demand.</summary>
/// <param name="Session">The session of the database, or the root session of the server when <paramref name="Database"/> is set.</param>
/// <param name="Database">The database to open on the server of <paramref name="Session"/>; <see langword="null"/> when <paramref name="Session"/> is already on it.</param>
public sealed record CompareEndpoint(ConnectionSession Session, string? Database)
{
    /// <summary>Gets the engine of the endpoint.</summary>
    public DatabaseDialect Dialect => Session.Profile.Dialect;

    /// <summary>Gets the environment of the connection, which the head turns into the pill and the colour stripe.</summary>
    public ConnectionEnvironment Environment => Session.Profile.Environment;

    /// <summary>Gets the name of the database the endpoint reads.</summary>
    public string DatabaseName => Database ?? Session.Profile.Database ?? "";

    /// <summary>Gets the connection name followed by the database when there is one.</summary>
    public string Label => DatabaseName.Length == 0 ? Session.Profile.Name : $"{Session.Profile.Name} · {DatabaseName}";

    /// <summary>Lists what can be compared among the open sessions.</summary>
    /// <param name="sessions">Every open session, including those opened on other databases of a server.</param>
    /// <returns>The sessions themselves, then the other databases of the servers that list several; Oracle is left out.</returns>
    public static IReadOnlyList<CompareEndpoint> List(IEnumerable<ConnectionSession> sessions)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        var all = sessions.Where(s => s.Profile.Dialect != DatabaseDialect.Oracle).ToList();
        var result = all.Select(s => new CompareEndpoint(s, null)).ToList();
        foreach (var root in all.Where(s => s.Parent is null && s.Databases.Count > 1))
        {
            var opened = all.Where(s => s.Root == root).Select(s => s.Profile.Database).ToList();
            result.AddRange(root.Databases
                .Where(db => !opened.Contains(db, StringComparer.OrdinalIgnoreCase))
                .Select(db => new CompareEndpoint(root, db)));
        }

        return result;
    }

    /// <summary>Tells whether at least two endpoints of one engine exist.</summary>
    /// <param name="endpoints">The result of <see cref="List"/>.</param>
    /// <returns><see langword="true"/> when a comparison can be opened.</returns>
    public static bool CanCompare(IReadOnlyList<CompareEndpoint> endpoints) =>
        endpoints.GroupBy(e => e.Dialect).Any(g => g.Count() >= 2);
}
