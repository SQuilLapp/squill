using Squill.Core;

namespace Squill.Sql.Functions;

/// <summary>The built-in functions known for each dialect.</summary>
public static class FunctionCatalog
{
    private static readonly FunctionSignature[] Common =
    [
        F("COUNT", "expr", "bigint", "Counts rows or non-null values."),
        F("SUM", "expr", "number", "Sums the values."),
        F("AVG", "expr", "number", "Averages the values."),
        F("MIN", "expr", "any", "Smallest value."),
        F("MAX", "expr", "any", "Largest value."),
        F("COALESCE", "value, fallback...", "any", "First non-null argument."),
        F("NULLIF", "a, b", "any", "NULL when both arguments are equal."),
        F("CAST", "expr AS type", "type", "Converts a value to another type."),
        F("SUBSTRING", "text, start, length", "text", "Extracts part of a string."),
        F("UPPER", "text", "text", "Converts to upper case."),
        F("LOWER", "text", "text", "Converts to lower case."),
        F("TRIM", "text", "text", "Removes surrounding blanks."),
        F("REPLACE", "text, from, to", "text", "Replaces every occurrence of a substring."),
        F("ABS", "number", "number", "Absolute value."),
        F("ROUND", "number, digits", "number", "Rounds to a number of digits."),
        F("CURRENT_DATE", "", "date", "Current date."),
    ];

    private static readonly FunctionSignature[] Postgres =
    [
        F("LENGTH", "text", "int", "Number of characters."),
        F("date_trunc", "field, timestamp", "timestamp", "Truncates a timestamp to a precision."),
        F("string_agg", "expr, delimiter", "text", "Concatenates values with a delimiter."),
        F("array_agg", "expr", "array", "Collects values into an array."),
        F("now", "", "timestamptz", "Current transaction timestamp."),
        F("extract", "field FROM source", "numeric", "Extracts a date/time field."),
        F("to_char", "value, format", "text", "Formats a value as text."),
        F("generate_series", "start, stop, step", "setof", "Generates a series of values."),
        F("regexp_replace", "text, pattern, replacement", "text", "Replaces by regular expression."),
        F("jsonb_build_object", "key, value...", "jsonb", "Builds a JSON object."),
        F("split_part", "text, delimiter, n", "text", "Returns the nth part of a split string."),
    ];

    private static readonly FunctionSignature[] MySql =
    [
        F("LENGTH", "text", "int", "Number of bytes."),
        F("DATE_FORMAT", "date, format", "text", "Formats a date."),
        F("IFNULL", "expr, fallback", "any", "Fallback when the expression is NULL."),
        F("NOW", "", "datetime", "Current date and time."),
        F("CONCAT", "text...", "text", "Concatenates strings."),
        F("GROUP_CONCAT", "expr", "text", "Concatenates the values of a group."),
        F("DATE_ADD", "date, INTERVAL n unit", "date", "Adds an interval to a date."),
        F("STR_TO_DATE", "text, format", "datetime", "Parses a date."),
        F("IF", "condition, then, else", "any", "Conditional value."),
        F("JSON_EXTRACT", "json, path", "json", "Extracts a JSON value."),
        F("CHAR_LENGTH", "text", "int", "Number of characters."),
    ];

    private static readonly FunctionSignature[] TSql =
    [
        F("LEN", "text", "int", "Number of characters."),
        F("GETDATE", "", "datetime", "Current date and time."),
        F("ISNULL", "expr, fallback", "any", "Fallback when the expression is NULL."),
        F("DATEADD", "part, n, date", "datetime", "Adds an interval to a date."),
        F("DATEDIFF", "part, start, end", "int", "Difference between two dates."),
        F("CONVERT", "type, expr, style", "type", "Converts a value to another type."),
        F("STRING_AGG", "expr, delimiter", "text", "Concatenates values with a delimiter."),
        F("CHARINDEX", "needle, text, start", "int", "Position of a substring."),
        F("FORMAT", "value, format", "text", "Formats a value."),
        F("NEWID", "", "uniqueidentifier", "New random GUID."),
        F("DATEPART", "part, date", "int", "Extracts a date part."),
    ];

    private static readonly FunctionSignature[] Sqlite =
    [
        F("LENGTH", "text", "int", "Number of characters."),
        F("strftime", "format, time, modifier...", "text", "Formats a date and time."),
        F("ifnull", "expr, fallback", "any", "Fallback when the expression is NULL."),
        F("date", "time, modifier...", "text", "Date part of a time value."),
        F("datetime", "time, modifier...", "text", "Date and time of a time value."),
        F("group_concat", "expr, separator", "text", "Concatenates the values of a group."),
        F("substr", "text, start, length", "text", "Extracts part of a string."),
        F("instr", "text, needle", "int", "Position of a substring."),
        F("typeof", "expr", "text", "Storage class of a value."),
        F("random", "", "int", "Pseudo-random integer."),
        F("json_extract", "json, path", "any", "Extracts a JSON value."),
    ];

    private static readonly Dictionary<DatabaseDialect, FunctionSignature[]> ByDialect = new()
    {
        [DatabaseDialect.PostgreSql] = [.. Common, .. Postgres],
        [DatabaseDialect.MySql] = [.. Common, .. MySql],
        [DatabaseDialect.SqlServer] = [.. Common, .. TSql],
        [DatabaseDialect.Sqlite] = [.. Common, .. Sqlite],
    };

    /// <summary>Lists the functions of a dialect: the common ones, then those specific to the engine.</summary>
    /// <param name="dialect">The database engine.</param>
    /// <returns>A shared, immutable list.</returns>
    public static IReadOnlyList<FunctionSignature> For(DatabaseDialect dialect) =>
        ByDialect.TryGetValue(dialect, out var list) ? list : Common;

    private static FunctionSignature F(string name, string parameters, string returns, string description) =>
        new(name, parameters.Length == 0 ? [] : parameters.Split(", "), returns, description);
}
