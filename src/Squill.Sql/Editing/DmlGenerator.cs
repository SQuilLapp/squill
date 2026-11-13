using Squill.Core;
using Squill.Core.Editing;
using Squill.Sql.Ddl;

namespace Squill.Sql.Editing;

/// <summary>Generates parameterized INSERT, UPDATE and DELETE statements from row edits.</summary>
public static class DmlGenerator
{
    /// <summary>Generates the statement that applies a row edit.</summary>
    /// <param name="edit">The edit to apply.</param>
    /// <param name="target">The table the edit applies to.</param>
    /// <param name="dialect">The target engine.</param>
    /// <returns>The statement; values are bound as <c>@p0</c>, <c>@p1</c>, ... and never inlined.</returns>
    /// <exception cref="ArgumentException">The edit does not match the target (key, empty changes, unknown column).</exception>
    public static DmlStatement Generate(RowEdit edit, EditTarget target, DatabaseDialect dialect) =>
        Generate(edit, target, dialect, null);

    /// <summary>Plans an INSERT and the way to read back the key the database generates for it.</summary>
    /// <param name="edit">The row to insert.</param>
    /// <param name="target">The table the row goes into.</param>
    /// <param name="dialect">The target engine.</param>
    /// <returns>The insert (returning the key itself where the engine allows) and, for MySQL only, the follow-up key query.</returns>
    /// <exception cref="ArgumentException">The edit does not match the target.</exception>
    /// <exception cref="NotSupportedException">More than one key column is generated.</exception>
    public static InsertPlan PlanInsert(InsertRow edit, EditTarget target, DatabaseDialect dialect)
    {
        ArgumentNullException.ThrowIfNull(edit);
        ArgumentNullException.ThrowIfNull(target);
        var generated = target.KeyColumns
            .Where(k => target.Columns.Any(c => c.Definition.IsAutoIncrement && string.Equals(c.Definition.Name, k, StringComparison.Ordinal))
                && !edit.Values.Any(v => string.Equals(v.Column, k, StringComparison.Ordinal) && v.Value is not SqlDefault))
            .ToList();
        if (generated.Count == 0)
            return new InsertPlan(Generate(edit, target, dialect, null), null);
        if (generated.Count > 1)
            throw new NotSupportedException("composite generated keys are not supported");
        return dialect == DatabaseDialect.MySql
            ? new InsertPlan(Generate(edit, target, dialect, null), new DmlStatement("SELECT LAST_INSERT_ID()", []))
            : new InsertPlan(Generate(edit, target, dialect, SqlQuoting.Quote(generated[0], dialect)), null);
    }

    /// <summary>Whether the engine returns the generated key from the INSERT itself rather than a follow-up query.</summary>
    /// <param name="d">The target engine.</param>
    /// <returns>True for PostgreSQL, SQLite and SQL Server; false for MySQL.</returns>
    public static bool ReturnsKeyFromInsert(DatabaseDialect d) => d != DatabaseDialect.MySql;

    private static DmlStatement Generate(RowEdit edit, EditTarget target, DatabaseDialect dialect, string? returnedKey)
    {
        ArgumentNullException.ThrowIfNull(edit);
        ArgumentNullException.ThrowIfNull(target);
        var table = SqlQuoting.QualifiedName(target.Schema, target.Table, dialect);
        var ps = new List<DmlParameter>();

        string Bind(object? value)
        {
            var name = "p" + ps.Count;
            ps.Add(new DmlParameter(name, value));
            return "@" + name;
        }

        // Typed text parameters need an explicit cast on PostgreSQL (json/jsonb); MySQL json and SQL Server xml accept a string.
        string BindFor(string column, object? value)
        {
            var bound = Bind(value);
            var type = target.Columns.FirstOrDefault(c => string.Equals(c.Definition.Name, column, StringComparison.Ordinal))?.Definition.DataType;
            return ParameterCast(dialect, type) is { } cast ? bound + cast : bound;
        }

        string Col(string name) => SqlQuoting.Quote(name, dialect);

        // SQLite has no DEFAULT keyword in an UPDATE: the default expression of the column is written out instead.
        string DefaultOf(string column) => dialect == DatabaseDialect.Sqlite
            ? target.Columns.FirstOrDefault(c => string.Equals(c.Definition.Name, column, StringComparison.Ordinal))?.Definition.DefaultExpression ?? "NULL"
            : "DEFAULT";

        string KeyPredicate(IReadOnlyList<ColumnValue> key)
        {
            if (!key.Select(k => k.Column).SequenceEqual(target.KeyColumns, StringComparer.Ordinal))
                throw new ArgumentException("The key must hold exactly the key columns of the target, in order.", nameof(edit));
            CheckColumns(key, target);
            return string.Join(" AND ", key.Select(k => k.Value is null ? $"{Col(k.Column)} IS NULL" : $"{Col(k.Column)} = {BindFor(k.Column, k.Value)}"));
        }

        string sql;
        switch (edit)
        {
            case InsertRow ins:
                CheckColumns(ins.Values, target);
                // A column left to its default is simply not listed.
                var given = ins.Values.Where(v => v.Value is not SqlDefault).ToList();
                sql = given.Count == 0
                    ? dialect == DatabaseDialect.MySql ? $"INSERT INTO {table} () VALUES ()" : $"INSERT INTO {table} DEFAULT VALUES"
                    : $"INSERT INTO {table} ({string.Join(", ", given.Select(v => Col(v.Column)))}) VALUES ({string.Join(", ", given.Select(v => BindFor(v.Column, v.Value)))})";
                // SQL Server refuses OUTPUT without INTO on a table with a trigger (error 334): SCOPE_IDENTITY() ignores the trigger's own inserts.
                if (returnedKey is not null)
                    sql += dialect == DatabaseDialect.SqlServer ? "; SELECT SCOPE_IDENTITY()" : $" RETURNING {returnedKey}";
                break;
            case UpdateRow upd:
                if (upd.Changes.Count == 0)
                    throw new ArgumentException("An update needs at least one change.", nameof(edit));
                CheckColumns(upd.Changes, target);
                var set = string.Join(", ", upd.Changes.Select(c => $"{Col(c.Column)} = {(c.Value is SqlDefault ? DefaultOf(c.Column) : BindFor(c.Column, c.Value))}"));
                sql = $"UPDATE {table} SET {set} WHERE {KeyPredicate(upd.Key)}";
                break;
            case DeleteRow del:
                sql = $"DELETE FROM {table} WHERE {KeyPredicate(del.Key)}";
                break;
            default:
                throw new ArgumentException("Unknown row edit.", nameof(edit));
        }

        return new DmlStatement(sql, ps);
    }

    // Suffix appended to a bound parameter so the engine converts the text to the column type. A text parameter against a PostgreSQL char(n) column
    // would compare as text, which drops the padding of the stored value and never matches it.
    private static string? ParameterCast(DatabaseDialect dialect, string? dataType)
    {
        if (dialect != DatabaseDialect.PostgreSql || dataType is not { } t)
            return null;
        if (t.Equals("json", StringComparison.OrdinalIgnoreCase) || t.Equals("jsonb", StringComparison.OrdinalIgnoreCase))
            return "::" + t.ToLowerInvariant();
        return t.StartsWith("char(", StringComparison.OrdinalIgnoreCase) || t.StartsWith("character(", StringComparison.OrdinalIgnoreCase) || t.Equals("bpchar", StringComparison.OrdinalIgnoreCase)
            ? "::bpchar"
            : null;
    }

    private static void CheckColumns(IReadOnlyList<ColumnValue> values, EditTarget target)
    {
        foreach (var v in values)
        {
            if (!target.Columns.Any(c => string.Equals(c.Definition.Name, v.Column, StringComparison.Ordinal)))
                throw new ArgumentException($"Column '{v.Column}' is not a column of the target.", nameof(values));
        }
    }
}
