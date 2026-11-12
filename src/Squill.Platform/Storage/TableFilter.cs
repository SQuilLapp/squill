namespace Squill.Platform.Storage;

/// <summary>The tables and views the user hid in the explorer, per database of one connection profile.</summary>
/// <param name="Hidden">Table keys (<c>schema.name</c>, or <c>name</c> without a schema) keyed by database name; read back with case-insensitive comparers.</param>
public sealed record TableFilter(IReadOnlyDictionary<string, IReadOnlyList<string>> Hidden);
