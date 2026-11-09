namespace Squill.Core;

/// <summary>The release stream a version belongs to, and a user can follow for updates.</summary>
public enum UpdateChannel
{
    /// <summary>Releases without a pre-release suffix.</summary>
    Stable,

    /// <summary>Pre-releases, plus every stable release.</summary>
    Beta,
}

/// <summary>Helpers that map versions and channels to their identifiers.</summary>
public static class UpdateChannelExtensions
{
    /// <summary>Gets the channel a version is published in.</summary>
    /// <param name="version">The version to classify.</param>
    /// <returns><see cref="UpdateChannel.Beta"/> for a pre-release, otherwise <see cref="UpdateChannel.Stable"/>.</returns>
    public static UpdateChannel ChannelOf(this SemanticVersion version) =>
        version.IsPreRelease ? UpdateChannel.Beta : UpdateChannel.Stable;

    /// <summary>Gets the lowercase identifier used in feed names; never translated.</summary>
    /// <param name="channel">The channel.</param>
    /// <returns><c>stable</c> or <c>beta</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="channel"/> is not a defined value.</exception>
    public static string ChannelId(this UpdateChannel channel) => channel switch
    {
        UpdateChannel.Stable => "stable",
        UpdateChannel.Beta => "beta",
        _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, null),
    };
}
