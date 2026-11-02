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
                && !edit.Values.Any(v => string.Equals(v.Column, k, StringComparison.Ordinal)))
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

        string Col(string name) => SqlQuoting.Quote(name, dialect);

        string KeyPredicate(IReadOnlyList<ColumnValue> key)
        {
            if (!key.Select(k => k.Column).SequenceEqual(target.KeyColumns, StringComparer.Ordinal))
                throw new ArgumentException("The key must hold exactly the key columns of the target, in order.", nameof(edit));
            CheckColumns(key, target);
            return string.Join(" AND ", key.Select(k => k.Value is null ? $"{Col(k.Column)} IS NULL" : $"{Col(k.Column)} = {Bind(k.Value)}"));
        }

        string sql;
        switch (edit)
        {
            case InsertRow ins:
                CheckColumns(ins.Values, target);
                var output = returnedKey is not null && dialect == DatabaseDialect.SqlServer ? $" OUTPUT INSERTED.{returnedKey}" : "";
                sql = ins.Values.Count == 0
                    ? dialect == DatabaseDialect.MySql ? $"INSERT INTO {table} () VALUES ()" : $"INSERT INTO {table}{output} DEFAULT VALUES"
                    : $"INSERT INTO {table} ({string.Join(", ", ins.Values.Select(v => Col(v.Column)))}){output} VALUES ({string.Join(", ", ins.Values.Select(v => Bind(v.Value)))})";
                if (returnedKey is not null && dialect != DatabaseDialect.SqlServer)
                    sql += $" RETURNING {returnedKey}";
                break;
            case UpdateRow upd:
                if (upd.Changes.Count == 0)
                    throw new ArgumentException("An update needs at least one change.", nameof(edit));
                CheckColumns(upd.Changes, target);
                var set = string.Join(", ", upd.Changes.Select(c => $"{Col(c.Column)} = {Bind(c.Value)}"));
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

    private static void CheckColumns(IReadOnlyList<ColumnValue> values, EditTarget target)
    {
        foreach (var v in values)
        {
            if (!target.Columns.Any(c => string.Equals(c.Definition.Name, v.Column, StringComparison.Ordinal)))
                throw new ArgumentException($"Column '{v.Column}' is not a column of the target.", nameof(values));
        }
    }
}
