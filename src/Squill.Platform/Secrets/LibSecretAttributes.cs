namespace Squill.Platform.Secrets;

/// <summary>Builds the Secret Service attribute set that identifies one SQuilL secret.</summary>
internal static class LibSecretAttributes
{
    /// <summary>Gets the attributes (<c>application</c> and <c>key</c>) that look up a secret.</summary>
    /// <param name="key">Secret key.</param>
    /// <returns>The attribute name/value pairs.</returns>
    public static IReadOnlyDictionary<string, string> For(string key) =>
        new Dictionary<string, string> { ["application"] = "squill", ["key"] = key };
}
