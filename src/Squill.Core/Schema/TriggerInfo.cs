namespace Squill.Core;

/// <summary>A trigger attached to a table. Supported by all four engines (SQLite, PostgreSQL, MySQL, SQL Server).</summary>
/// <param name="Schema">Schema (or database) owning the table; empty when the engine has none.</param>
/// <param name="Table">Name of the table the trigger fires on.</param>
/// <param name="Name">Trigger name.</param>
/// <param name="Timing">When it fires, as the engine reports it (<c>BEFORE</c>, <c>AFTER</c>, <c>INSTEAD OF</c>).</param>
/// <param name="Events">Events that fire it, e.g. <c>INSERT OR UPDATE</c>.</param>
/// <param name="Definition">Source text of the trigger; null when not read.</param>
public sealed record TriggerInfo(
    string Schema,
    string Table,
    string Name,
    string Timing,
    string Events,
    string? Definition);
