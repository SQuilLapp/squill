namespace Squill.Core;

/// <summary>SQL spelling of <see cref="ReferentialAction"/> values.</summary>
public static class ReferentialActions
{
    /// <summary>Returns the SQL keywords for an action.</summary>
    /// <param name="action">The action.</param>
    /// <returns>For example <c>SET NULL</c>.</returns>
    public static string ToSql(this ReferentialAction action) => action switch
    {
        ReferentialAction.Restrict => "RESTRICT",
        ReferentialAction.Cascade => "CASCADE",
        ReferentialAction.SetNull => "SET NULL",
        ReferentialAction.SetDefault => "SET DEFAULT",
        _ => "NO ACTION",
    };

    /// <summary>Parses the SQL keywords of an action, ignoring case and extra whitespace.</summary>
    /// <param name="sql">Text such as <c>set  null</c>.</param>
    /// <returns>The action, or <see langword="null"/> when the text is null or unknown.</returns>
    public static ReferentialAction? Parse(string? sql)
    {
        if (sql is null)
        {
            return null;
        }

        var normalized = string.Join(' ', sql.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return normalized.ToUpperInvariant() switch
        {
            "NO ACTION" => ReferentialAction.NoAction,
            "RESTRICT" => ReferentialAction.Restrict,
            "CASCADE" => ReferentialAction.Cascade,
            "SET NULL" => ReferentialAction.SetNull,
            "SET DEFAULT" => ReferentialAction.SetDefault,
            _ => null,
        };
    }
}
