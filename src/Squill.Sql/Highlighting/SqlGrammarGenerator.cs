using System.Collections.Frozen;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Squill.Core;
using Squill.Sql.Functions;
using Squill.Sql.Keywords;

namespace Squill.Sql.Highlighting;

/// <summary>Generates the TextMate grammar of a dialect from its keyword set and function catalog.</summary>
public static class SqlGrammarGenerator
{
    /// <summary>Generates the grammar JSON; the output is deterministic and uses LF line endings.</summary>
    /// <param name="dialect">The database engine.</param>
    /// <returns>The text of a <c>.tmLanguage.json</c> file, ending with a newline.</returns>
    public static string Generate(DatabaseDialect dialect)
    {
        var name = dialect switch
        {
            DatabaseDialect.PostgreSql => "postgresql",
            DatabaseDialect.MySql => "mysql",
            DatabaseDialect.SqlServer => "tsql",
            _ => "sqlite",
        };

        FrozenSet<string> keywordSet = dialect switch
        {
            DatabaseDialect.PostgreSql => PostgresKeywords.Words,
            DatabaseDialect.MySql => MySqlKeywords.Words,
            DatabaseDialect.SqlServer => TSqlKeywords.Words,
            _ => SqliteKeywords.Words,
        };

        var keywords = keywordSet.Select(k => k.ToUpperInvariant()).Distinct().Order(StringComparer.Ordinal).ToArray();
        var functions = FunctionCatalog.For(dialect)
            .Select(f => f.Name.ToUpperInvariant())
            .Where(f => !keywordSet.Contains(f))
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToArray();

        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = true,
            NewLine = "\n",
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }))
        {
            w.WriteStartObject();
            w.WriteString("name", $"SQL ({name})");
            w.WriteString("scopeName", $"source.sql.{name}");
            w.WriteStartArray("patterns");

            Match(w, "comment.line.double-dash.sql", dialect == DatabaseDialect.MySql ? "(--|#).*$" : "--.*$");
            Range(w, "comment.block.sql", "/\\*", "\\*/");

            switch (dialect)
            {
                case DatabaseDialect.PostgreSql:
                    Range(w, "string.quoted.dollar.sql", "\\$([A-Za-z_][A-Za-z0-9_]*)?\\$", "\\$\\1\\$");
                    Range(w, "string.quoted.single.sql", "[Ee]'", "'", "\\\\.|''");
                    Range(w, "string.quoted.single.sql", "'", "'", "''");
                    Range(w, "variable.other.quoted.sql", "\"", "\"", "\"\"");
                    break;
                case DatabaseDialect.MySql:
                    Range(w, "string.quoted.single.sql", "'", "'", "\\\\.|''");
                    Range(w, "string.quoted.double.sql", "\"", "\"", "\\\\.|\"\"");
                    Range(w, "variable.other.quoted.sql", "`", "`", "``");
                    break;
                case DatabaseDialect.SqlServer:
                    Range(w, "string.quoted.single.sql", "[Nn]?'", "'", "''");
                    Range(w, "variable.other.quoted.sql", "\"", "\"", "\"\"");
                    Range(w, "variable.other.quoted.sql", "\\[", "\\]", "\\]\\]");
                    break;
                default:
                    Range(w, "string.quoted.single.sql", "'", "'", "''");
                    Range(w, "variable.other.quoted.sql", "\"", "\"", "\"\"");
                    Range(w, "variable.other.quoted.sql", "`", "`", "``");
                    Range(w, "variable.other.quoted.sql", "\\[", "\\]");
                    break;
            }

            var parameter = dialect switch
            {
                DatabaseDialect.PostgreSql => "\\$\\d+",
                DatabaseDialect.MySql => "\\?|@@?[A-Za-z_][A-Za-z0-9_$]*",
                DatabaseDialect.SqlServer => "@[A-Za-z_][A-Za-z0-9_$#]*",
                _ => "\\?\\d*|[:@$][A-Za-z_][A-Za-z0-9_]*",
            };
            Match(w, "variable.parameter.sql", parameter);
            Match(w, "constant.numeric.sql", "\\b\\d+(\\.\\d*)?([eE][+-]?\\d+)?\\b|\\.\\d+([eE][+-]?\\d+)?\\b");
            Match(w, "support.function.sql", $"(?i)\\b({string.Join('|', functions)})\\b(?=\\s*\\()");
            Match(w, "keyword.control.sql", $"(?i)\\b({string.Join('|', keywords)})\\b");
            Match(w, "keyword.operator.sql", "<=|>=|<>|!=|\\|\\||[-+*/%=<>]" + (dialect == DatabaseDialect.PostgreSql ? "|::" : string.Empty));

            w.WriteEndArray();
            w.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }

    private static void Match(Utf8JsonWriter w, string scope, string pattern)
    {
        w.WriteStartObject();
        w.WriteString("name", scope);
        w.WriteString("match", pattern);
        w.WriteEndObject();
    }

    private static void Range(Utf8JsonWriter w, string scope, string begin, string end, string? escape = null)
    {
        w.WriteStartObject();
        w.WriteString("name", scope);
        w.WriteString("begin", begin);
        w.WriteString("end", end);
        if (escape is not null)
        {
            w.WriteStartArray("patterns");
            Match(w, "constant.character.escape.sql", escape);
            w.WriteEndArray();
        }

        w.WriteEndObject();
    }
}
