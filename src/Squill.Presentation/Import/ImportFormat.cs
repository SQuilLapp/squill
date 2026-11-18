namespace Squill.Presentation.Import;

/// <summary>How the text of a CSV field is read into a typed value.</summary>
public sealed record ImportFormat
{
    /// <summary>Gets the format that reads ISO dates and invariant numbers.</summary>
    public static ImportFormat Default { get; } = new();

    /// <summary>Gets the decimal separator, <c>.</c> or <c>,</c>.</summary>
    public char DecimalSeparator { get; init; } = '.';

    /// <summary>Gets the thousands separator removed from numbers, or <c>null</c> for none; a space also removes no-break spaces.</summary>
    public char? ThousandsSeparator { get; init; }

    /// <summary>Gets the exact date format such as <c>dd/MM/yyyy HH:mm</c>, or <c>null</c> for ISO.</summary>
    public string? DateFormat { get; init; }

    /// <summary>Gets the IANA time-zone name the dates are written in, or <c>null</c> to leave them as written.</summary>
    public string? TimeZoneId { get; init; }

    /// <summary>Gets the words read as <c>true</c> (case-insensitive).</summary>
    public IReadOnlyList<string> TrueValues { get; init; } = ["true", "1", "yes", "t"];

    /// <summary>Gets the words read as <c>false</c> (case-insensitive).</summary>
    public IReadOnlyList<string> FalseValues { get; init; } = ["false", "0", "no", "f"];

    /// <summary>Gets a value indicating whether an empty field becomes NULL.</summary>
    public bool EmptyAsNull { get; init; } = true;

    /// <summary>Gets a text that is read as NULL (for example <c>NULL</c>), or <c>null</c> for none.</summary>
    public string? NullToken { get; init; }

    /// <summary>Resolves a time-zone name.</summary>
    /// <param name="id">IANA name, or <c>null</c>/empty for none.</param>
    /// <param name="zone">The zone, or <c>null</c> when none was asked for or the name is unknown.</param>
    /// <returns><c>false</c> when a name was given but is unknown (the option must be rejected).</returns>
    public static bool TryResolveTimeZone(string? id, out TimeZoneInfo? zone)
    {
        zone = null;
        if (string.IsNullOrWhiteSpace(id))
        {
            return true;
        }

        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(id);
            return true;
        }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return false;
        }
    }
}
