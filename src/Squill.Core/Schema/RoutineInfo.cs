namespace Squill.Core;

/// <summary>A stored function or procedure. SQLite has no routines; PostgreSQL, MySQL and SQL Server do.</summary>
/// <param name="Schema">Schema (or database) owning the routine.</param>
/// <param name="Name">Routine name.</param>
/// <param name="Kind">Function or procedure.</param>
/// <param name="Signature">Name and argument types as the engine reports them, e.g. <c>add(integer, integer)</c>.</param>
/// <param name="ReturnType">Return type of a function; null for procedures.</param>
/// <param name="Definition">Source text of the routine; null when the engine hides it or it was not read.</param>
public sealed record RoutineInfo(
    string Schema,
    string Name,
    RoutineKind Kind,
    string Signature,
    string? ReturnType,
    string? Definition);
