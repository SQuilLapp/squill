using System.Text;

namespace Squill.Presentation.Import;

/// <summary>How a CSV file is decoded and split into fields.</summary>
/// <param name="Encoding">Text encoding; only UTF-8, UTF-16 and Latin-1 are used (no code-page provider under AOT).</param>
/// <param name="Delimiter">Field separator.</param>
/// <param name="Quote">Quote character; a doubled quote inside a quoted field is a literal quote.</param>
/// <param name="HasHeader">Whether the first row holds column names.</param>
public sealed record CsvOptions(Encoding Encoding, char Delimiter, char Quote, bool HasHeader)
{
    /// <summary>Gets comma-separated UTF-8 options with a header row.</summary>
    public static CsvOptions Default { get; } = new(new UTF8Encoding(false), ',', '"', true);
}
