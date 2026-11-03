namespace Squill.Platform.Settings;

/// <summary>Display language requested by the user.</summary>
public enum AppLanguage
{
    /// <summary>Follow the operating system UI language (French when it is French, English otherwise).</summary>
    System,

    /// <summary>Always use French.</summary>
    French,

    /// <summary>Always use English.</summary>
    English,
}
