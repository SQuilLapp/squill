namespace Squill.Platform.Profiles;

/// <summary>A connection profile that was opened recently.</summary>
/// <param name="ProfileId">Identifier of the profile.</param>
/// <param name="LastOpened">When the profile was last opened.</param>
public sealed record RecentConnection(Guid ProfileId, DateTimeOffset LastOpened);
