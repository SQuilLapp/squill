namespace Squill.Presentation.Import;

/// <summary>A row that could not be converted.</summary>
/// <param name="LineNumber">The line in the file where the row starts.</param>
/// <param name="ColumnName">The table column that failed.</param>
/// <param name="Issue">The failure.</param>
/// <param name="RawText">The field text as read.</param>
public sealed record RowRejection(long LineNumber, string ColumnName, ConversionIssue Issue, string? RawText);

/// <summary>A converted row, or the reason it was rejected.</summary>
/// <param name="Values">Typed values for the imported columns in mapping order; <c>null</c> when rejected.</param>
/// <param name="Rejection">Why the row was rejected, otherwise <c>null</c>.</param>
public sealed record ImportRow(object?[]? Values, RowRejection? Rejection);

/// <summary>Converts CSV rows to typed rows following a mapping.</summary>
/// <param name="mapping">The user's mapping.</param>
/// <param name="format">The reading options.</param>
public sealed class ImportRowConverter(ImportMapping mapping, ImportFormat format)
{
    private readonly ColumnMap[] _active = [.. mapping.Active];

    /// <summary>Converts one CSV row; a missing field reads as NULL.</summary>
    /// <param name="fields">The row's fields.</param>
    /// <param name="lineNumber">The line the row starts at.</param>
    /// <returns>The typed values or the first rejection.</returns>
    public ImportRow Convert(string?[] fields, long lineNumber)
    {
        ArgumentNullException.ThrowIfNull(fields);
        var values = new object?[_active.Length];
        for (var i = 0; i < _active.Length; i++)
        {
            var m = _active[i];
            var raw = m.SourceIndex < fields.Length ? fields[m.SourceIndex] : null;
            var result = ValueConverter.Convert(raw, m.Target!, format);
            if (result.Issue is { } issue)
            {
                return new(null, new RowRejection(lineNumber, m.Target!.Name, issue, raw));
            }

            values[i] = result.Value;
        }

        return new(values, null);
    }
}
