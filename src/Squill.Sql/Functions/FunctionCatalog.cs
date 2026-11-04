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
        F("concat", "text...", "text", "Concatenates strings, ignoring NULL."),
        F("concat_ws", "separator, text...", "text", "Concatenates strings with a separator."),
        F("left", "text, n", "text", "First n characters."),
        F("right", "text, n", "text", "Last n characters."),
        F("lpad", "text, length, fill", "text", "Pads on the left to a length."),
        F("rpad", "text, length, fill", "text", "Pads on the right to a length."),
        F("position", "needle IN text", "int", "Position of a substring."),
        F("age", "timestamp, timestamp", "interval", "Difference between two timestamps as an interval."),
        F("to_date", "text, format", "date", "Parses a date."),
        F("floor", "number", "number", "Largest integer not above the value."),
        F("ceil", "number", "number", "Smallest integer not below the value."),
        F("power", "base, exponent", "number", "Raises to a power."),
        F("sqrt", "number", "number", "Square root."),
        F("mod", "a, b", "number", "Remainder of a division."),
        F("random", "", "double", "Pseudo-random number between 0 and 1."),
        F("greatest", "value...", "any", "Largest argument."),
        F("least", "value...", "any", "Smallest argument."),
        F("json_agg", "expr", "json", "Collects values into a JSON array."),
        F("unnest", "array", "setof", "Expands an array into rows."),
        F("row_number", "", "bigint", "Sequential number of the row within its window partition."),
        F("rank", "", "bigint", "Rank of the row within its partition, with gaps after ties."),
        F("lag", "expr, offset, default", "any", "Value of a previous row of the partition."),
        F("lead", "expr, offset, default", "any", "Value of a following row of the partition."),
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
        F("CONCAT_WS", "separator, text...", "text", "Concatenates strings with a separator."),
        F("LEFT", "text, n", "text", "First n characters."),
        F("RIGHT", "text, n", "text", "Last n characters."),
        F("LPAD", "text, length, fill", "text", "Pads on the left to a length."),
        F("RPAD", "text, length, fill", "text", "Pads on the right to a length."),
        F("LOCATE", "needle, text, start", "int", "Position of a substring."),
        F("CURDATE", "", "date", "Current date."),
        F("DATE_SUB", "date, INTERVAL n unit", "date", "Subtracts an interval from a date."),
        F("DATEDIFF", "end, start", "int", "Number of days between two dates."),
        F("EXTRACT", "unit FROM date", "int", "Extracts a date/time field."),
        F("FLOOR", "number", "number", "Largest integer not above the value."),
        F("CEIL", "number", "number", "Smallest integer not below the value."),
        F("POWER", "base, exponent", "number", "Raises to a power."),
        F("SQRT", "number", "number", "Square root."),
        F("MOD", "a, b", "number", "Remainder of a division."),
        F("RAND", "", "double", "Pseudo-random number between 0 and 1."),
        F("GREATEST", "value...", "any", "Largest argument."),
        F("LEAST", "value...", "any", "Smallest argument."),
        F("SUBSTRING_INDEX", "text, delimiter, count", "text", "Part of a string before or after the nth delimiter."),
        F("ROW_NUMBER", "", "bigint", "Sequential number of the row within its window partition."),
        F("RANK", "", "bigint", "Rank of the row within its partition, with gaps after ties."),
        F("LAG", "expr, offset, default", "any", "Value of a previous row of the partition."),
        F("LEAD", "expr, offset, default", "any", "Value of a following row of the partition."),
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
        F("IIF", "condition, then, else", "any", "Conditional value."),
        F("LEFT", "text, n", "text", "First n characters."),
        F("RIGHT", "text, n", "text", "Last n characters."),
        F("CONCAT", "text...", "text", "Concatenates strings, ignoring NULL."),
        F("SYSDATETIME", "", "datetime2", "Current date and time with high precision."),
        F("EOMONTH", "date, months", "date", "Last day of the month."),
        F("TRY_CONVERT", "type, expr, style", "type", "Converts a value, NULL when it fails."),
        F("TRY_CAST", "expr AS type", "type", "Converts a value, NULL when it fails."),
        F("FLOOR", "number", "number", "Largest integer not above the value."),
        F("CEILING", "number", "number", "Smallest integer not below the value."),
        F("POWER", "base, exponent", "number", "Raises to a power."),
        F("SQRT", "number", "number", "Square root."),
        F("STUFF", "text, start, length, insert", "text", "Replaces part of a string."),
        F("REPLICATE", "text, n", "text", "Repeats a string."),
        F("STRING_SPLIT", "text, separator", "table", "Splits a string into rows."),
        F("OBJECT_ID", "name", "int", "Identifier of a database object."),
        F("ROW_NUMBER", "", "bigint", "Sequential number of the row within its window partition."),
        F("RANK", "", "bigint", "Rank of the row within its partition, with gaps after ties."),
        F("LAG", "expr, offset, default", "any", "Value of a previous row of the partition."),
        F("LEAD", "expr, offset, default", "any", "Value of a following row of the partition."),
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
        F("total", "expr", "double", "Sum that is 0.0 instead of NULL for no rows."),
        F("iif", "condition, then, else", "any", "Conditional value."),
        F("ltrim", "text, chars", "text", "Removes leading characters."),
        F("rtrim", "text, chars", "text", "Removes trailing characters."),
        F("time", "time, modifier...", "text", "Time part of a time value."),
        F("julianday", "time, modifier...", "real", "Julian day number of a time value."),
        F("json_object", "key, value...", "text", "Builds a JSON object."),
        F("json_array", "value...", "text", "Builds a JSON array."),
        F("printf", "format, value...", "text", "Formats values like C printf."),
        F("hex", "blob", "text", "Hexadecimal rendering of a blob."),
        F("quote", "value", "text", "SQL literal of a value."),
        F("last_insert_rowid", "", "int", "Rowid of the last inserted row."),
        F("row_number", "", "bigint", "Sequential number of the row within its window partition."),
        F("rank", "", "bigint", "Rank of the row within its partition, with gaps after ties."),
        F("lag", "expr, offset, default", "any", "Value of a previous row of the partition."),
        F("lead", "expr, offset, default", "any", "Value of a following row of the partition."),
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
