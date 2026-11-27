namespace Squill.Platform.Storage;

/// <summary>What the user hid in the explorer for one connection profile: databases, schemas and tables.</summary>
/// <param name="Hidden">Table keys (<c>schema.name</c>, or <c>name</c> without a schema) keyed by database name; read back with case-insensitive comparers.</param>
public sealed record TableFilter(IReadOnlyDictionary<string, IReadOnlyList<string>> Hidden)
{
    /// <summary>Visibility of the databases of the connection; <see langword="null"/> when nothing is filtered.</summary>
    public NameFilter? Databases { get; init; }

    /// <summary>Visibility of the schemas, keyed by database name.</summary>
    public IReadOnlyDictionary<string, NameFilter> Schemas { get; init; } = new Dictionary<string, NameFilter>();

    /// <summary>Wildcard patterns matched against table keys (<c>schema.name</c>), keyed by database name.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> TablePatterns { get; init; } = new Dictionary<string, IReadOnlyList<string>>();
}
