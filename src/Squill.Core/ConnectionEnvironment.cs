namespace Squill.Core;

/// <summary>
/// Kind of environment a connection points at, used only to color tabs, pills, stripes and the status bar.
/// Nothing behaves differently per environment yet (no confirmation, no manual transaction), even for <see cref="Production"/>.
/// </summary>
public enum ConnectionEnvironment
{
    /// <summary>No environment set.</summary>
    None,

    /// <summary>A production server.</summary>
    Production,

    /// <summary>A staging server.</summary>
    Staging,

    /// <summary>A shared development server.</summary>
    Development,

    /// <summary>A database on the user's own machine.</summary>
    Local,
}
