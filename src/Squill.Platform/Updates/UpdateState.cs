namespace Squill.Platform.Updates;

/// <summary>What the application remembers between update checks.</summary>
/// <param name="LastCheckUtc">When the last check ran, or <see langword="null"/> if none did.</param>
/// <param name="SkippedVersion">Version the user asked not to be notified about.</param>
/// <param name="LastSeenVersion">Newest version the last check returned; lets the banner show once per version.</param>
public sealed record UpdateState(DateTimeOffset? LastCheckUtc = null, string? SkippedVersion = null, string? LastSeenVersion = null);
