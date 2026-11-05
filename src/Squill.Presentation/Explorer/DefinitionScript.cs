using Squill.Core;
using Squill.Core.Design;
using Squill.Sql.Ddl;

namespace Squill.Presentation.Explorer;

/// <summary>
/// Builds the SQL text opened in a new tab for the "Open definition" and "Script as CREATE TABLE" explorer actions. Engines store
/// definitions differently: PostgreSQL and MySQL return only the body of a view, MySQL only the body of a routine or trigger; the
/// others return the full statement, which is used as stored.
/// </summary>
public static class DefinitionScript
{
    /// <summary>Builds the script of a view or materialized view.</summary>
    /// <param name="dialect">The engine of the connection.</param>
    /// <param name="view">The view; its <see cref="TableInfo.Definition"/> is read.</param>
    /// <returns>The script, or <see langword="null"/> when the engine returned no definition (for example an encrypted module).</returns>
    public static string? View(DatabaseDialect dialect, TableInfo view)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (string.IsNullOrWhiteSpace(view.Definition))
            return null;
        if (dialect is not (DatabaseDialect.PostgreSql or DatabaseDialect.MySql))
            return view.Definition;
        var keyword = view.Kind == TableKind.MaterializedView ? "CREATE MATERIALIZED VIEW" : "CREATE VIEW";
        return $"{keyword} {SqlQuoting.QualifiedName(view.Schema, view.Name, dialect)} AS\n{view.Definition.Trim()}";
    }

    /// <summary>Builds the script of a function or procedure.</summary>
    /// <param name="dialect">The engine of the connection.</param>
    /// <param name="routine">The routine.</param>
    /// <returns>The script, or <see langword="null"/> when the engine returned no definition.</returns>
    public static string? Routine(DatabaseDialect dialect, RoutineInfo routine)
    {
        ArgumentNullException.ThrowIfNull(routine);
        if (string.IsNullOrWhiteSpace(routine.Definition))
            return null;
        if (dialect != DatabaseDialect.MySql)
            return routine.Definition;
        var kind = routine.Kind == RoutineKind.Procedure ? "PROCEDURE" : "FUNCTION";
        var returns = routine.ReturnType is { Length: > 0 } type ? $"\nRETURNS {type}" : "";
        return $"CREATE {kind} {routine.Signature}{returns}\n{routine.Definition.Trim()}";
    }

    /// <summary>Builds the script of a trigger.</summary>
    /// <param name="dialect">The engine of the connection.</param>
    /// <param name="trigger">The trigger.</param>
    /// <returns>The script, or <see langword="null"/> when the engine returned no definition.</returns>
    public static string? Trigger(DatabaseDialect dialect, TriggerInfo trigger)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        if (string.IsNullOrWhiteSpace(trigger.Definition))
            return null;
        if (dialect != DatabaseDialect.MySql)
            return trigger.Definition;
        return $"CREATE TRIGGER {SqlQuoting.Quote(trigger.Name, dialect)} {trigger.Timing} {trigger.Events}\n"
            + $"ON {SqlQuoting.Quote(trigger.Table, dialect)} FOR EACH ROW\n{trigger.Definition.Trim()}";
    }

    /// <summary>Builds the <c>CREATE TABLE</c> script of an existing table, with its indexes and foreign keys.</summary>
    /// <param name="dialect">The engine of the connection.</param>
    /// <param name="table">The table.</param>
    /// <returns>The script.</returns>
    /// <exception cref="ArgumentException"><paramref name="table"/> is a view.</exception>
    public static string CreateTable(DatabaseDialect dialect, TableInfo table) =>
        DdlGenerators.ToScript(DdlGenerators.For(dialect).CreateTable(TableDesign.FromTable(table)));
}
