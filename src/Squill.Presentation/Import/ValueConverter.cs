using System.Globalization;
using System.Text.Json;
using Squill.Core;
using Squill.Presentation.Localization;
using Squill.Presentation.Results;

namespace Squill.Presentation.Import;

/// <summary>What is wrong with a value or a column.</summary>
public enum ColumnIssueKind
{
    /// <summary>Nothing is wrong.</summary>
    None,

    /// <summary>The text is not valid for the column type.</summary>
    Invalid,

    /// <summary>The text is not valid JSON.</summary>
    NotJson,

    /// <summary>The value is NULL but the column does not accept it.</summary>
    NullNotAllowed,

    /// <summary>The time-zone option is unknown.</summary>
    BadTimeZone,
}

/// <summary>A conversion failure.</summary>
/// <param name="Kind">The failure class.</param>
/// <param name="Message">A localized message for the user.</param>
public sealed record ConversionIssue(ColumnIssueKind Kind, string Message);

/// <summary>The outcome of converting one field.</summary>
/// <param name="Value">The typed value, <c>null</c> for SQL NULL or on failure.</param>
/// <param name="Issue">The failure, or <c>null</c> on success.</param>
public sealed record ConversionResult(object? Value, ConversionIssue? Issue);

/// <summary>Turns CSV text into the CLR value a provider accepts for a column.</summary>
public static class ValueConverter
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>Converts one field.</summary>
    /// <param name="text">The field text, or <c>null</c> when the field is missing.</param>
    /// <param name="target">The destination column.</param>
    /// <param name="format">The reading options.</param>
    /// <returns>The value or the issue.</returns>
    public static ConversionResult Convert(string? text, ColumnDefinition target, ImportFormat format)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(format);
        if (text is null || (format.NullToken is { } token && text == token) || (format.EmptyAsNull && text.Trim().Length == 0))
        {
            var required = !target.IsNullable && target.DefaultExpression is null && !target.IsAutoIncrement && !target.IsGenerated;
            return required ? Fail(ColumnIssueKind.NullNotAllowed, UiText.Format("import.issue.nullNotAllowed", target.Name)) : new(null, null);
        }

        if (!ImportFormat.TryResolveTimeZone(format.TimeZoneId, out var zone))
        {
            return Fail(ColumnIssueKind.BadTimeZone, UiText.Format("import.issue.badTimeZone", format.TimeZoneId));
        }

        var type = ClrTypeFor(target.DataType);
        if (type is null)
        {
            return Fail(ColumnIssueKind.Invalid, UiText.Format("import.issue.unsupportedType", target.DataType));
        }

        if (type == typeof(string))
        {
            return new(text, null);
        }

        if (type == typeof(JsonDocument))
        {
            return IsJson(text) ? new(text, null) : Fail(ColumnIssueKind.NotJson, UiText.Format("import.issue.notJson", text));
        }

        var s = text.Trim();
        if (type == typeof(bool))
        {
            var isTrue = format.TrueValues.Contains(s, StringComparer.OrdinalIgnoreCase);
            return isTrue || format.FalseValues.Contains(s, StringComparer.OrdinalIgnoreCase)
                ? new(isTrue, null)
                : Fail(ColumnIssueKind.Invalid, UiText.Format("results.invalidValue", text, "true/false"));
        }

        if (IsNumeric(type))
        {
            if (format.ThousandsSeparator is { } thousands)
            {
                s = s.Replace(thousands.ToString(), string.Empty, StringComparison.Ordinal);
                if (thousands == ' ')
                {
                    s = s.Replace(" ", string.Empty, StringComparison.Ordinal).Replace(" ", string.Empty, StringComparison.Ordinal);
                }
            }

            if (format.DecimalSeparator != '.')
            {
                s = s.Replace(format.DecimalSeparator, '.');
            }
        }
        else if (format.DateFormat is { } dateFormat && (type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(DateOnly)))
        {
            return ParseDate(s, text, type, dateFormat, zone, target);
        }

        return CellValueParser.TryParse(s, new ColumnInfo(target.Name, target.DataType, type, target.IsNullable, 0), out var value, out var error)
            ? new(value, null)
            : Fail(ColumnIssueKind.Invalid, error ?? text);
    }

    /// <summary>Gets the CLR type the providers accept for a database type name.</summary>
    /// <param name="dataType">The column's database type name.</param>
    /// <returns>The type (<see cref="JsonDocument"/> stands for JSON columns), or <c>null</c> when the text cannot be converted.</returns>
    public static Type? ClrTypeFor(string dataType)
    {
        ArgumentNullException.ThrowIfNull(dataType);
        var t = dataType.Trim().ToLowerInvariant();
        var paren = t.IndexOf('(', StringComparison.Ordinal);
        if (paren >= 0)
        {
            t = t[..paren].Trim();
        }

        if (t.EndsWith("[]", StringComparison.Ordinal) || t.StartsWith('_'))
        {
            var element = t.Replace("[]", string.Empty, StringComparison.Ordinal).TrimStart('_');
            return element is "int" or "integer" or "int4" ? typeof(int[]) : element is "text" or "varchar" or "character varying" ? typeof(string[]) : null;
        }

        return t switch
        {
            "bool" or "boolean" or "bit" => typeof(bool),
            "tinyint" or "smallint" or "int2" or "smallserial" => typeof(short),
            "int" or "integer" or "int4" or "mediumint" or "serial" => typeof(int),
            "bigint" or "int8" or "bigserial" => typeof(long),
            "decimal" or "numeric" or "money" or "smallmoney" or "number" => typeof(decimal),
            "float" or "real" or "double" or "double precision" or "float4" or "float8" => typeof(double),
            "datetimeoffset" => typeof(DateTimeOffset),
            "date" => typeof(DateOnly),
            "uuid" or "uniqueidentifier" => typeof(Guid),
            "bytea" or "blob" or "binary" or "varbinary" or "longblob" or "mediumblob" or "tinyblob" or "image" => typeof(byte[]),
            "json" or "jsonb" => typeof(JsonDocument),
            _ when t.StartsWith("timestamp", StringComparison.Ordinal) || t.StartsWith("datetime", StringComparison.Ordinal) || t == "smalldatetime" => typeof(DateTime),
            _ when t.StartsWith("time", StringComparison.Ordinal) => typeof(TimeOnly),
            _ => typeof(string),
        };
    }

    private static ConversionResult ParseDate(string s, string raw, Type type, string dateFormat, TimeZoneInfo? zone, ColumnDefinition target)
    {
        var bad = Fail(ColumnIssueKind.Invalid, UiText.Format("results.invalidValue", raw, dateFormat));
        if (type == typeof(DateOnly))
        {
            return DateOnly.TryParseExact(s, dateFormat, Invariant, DateTimeStyles.None, out var d) ? new(d, null) : bad;
        }

        if (!DateTime.TryParseExact(s, dateFormat, Invariant, DateTimeStyles.None, out var local))
        {
            return bad;
        }

        if (zone is null)
        {
            // Without a zone the text is taken as UTC for offset and timestamptz columns, as written otherwise.
            var tz = target.DataType.Contains("tz", StringComparison.OrdinalIgnoreCase) || target.DataType.Contains("with time zone", StringComparison.OrdinalIgnoreCase);
            var asUtc = DateTime.SpecifyKind(local, DateTimeKind.Utc);
            return new(type == typeof(DateTimeOffset) ? new DateTimeOffset(asUtc) : (object)(tz ? asUtc : local), null);
        }

        if (zone.IsInvalidTime(local))
        {
            return bad;
        }

        var utc = TimeZoneInfo.ConvertTimeToUtc(local, zone);
        return new(type == typeof(DateTimeOffset) ? new DateTimeOffset(utc) : (object)utc, null);
    }

    private static bool IsNumeric(Type t) => t == typeof(short) || t == typeof(int) || t == typeof(long) || t == typeof(decimal) || t == typeof(double);

    private static bool IsJson(string text)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static ConversionResult Fail(ColumnIssueKind kind, string message) => new(null, new ConversionIssue(kind, message));
}
