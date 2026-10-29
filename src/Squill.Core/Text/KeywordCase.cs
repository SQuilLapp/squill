namespace Squill.Core;

/// <summary>How SQL keywords are cased by the formatter.</summary>
public enum KeywordCase
{
    /// <summary>Keywords are written in upper case.</summary>
    Upper,

    /// <summary>Keywords are written in lower case.</summary>
    Lower,

    /// <summary>Keywords keep the casing typed by the user.</summary>
    Preserve,
}
