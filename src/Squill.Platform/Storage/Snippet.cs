namespace Squill.Platform.Storage;

/// <summary>A named, reusable piece of SQL.</summary>
/// <param name="Id">Stable identifier.</param>
/// <param name="Name">Display name.</param>
/// <param name="Sql">SQL text.</param>
public sealed record Snippet(Guid Id, string Name, string Sql);
