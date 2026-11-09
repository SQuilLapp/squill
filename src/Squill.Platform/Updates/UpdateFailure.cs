namespace Squill.Platform.Updates;

/// <summary>The reason an update operation failed, as mapped by the engine.</summary>
public enum UpdateFailure
{
    /// <summary>The feed or the download could not be reached.</summary>
    Network,

    /// <summary>The feed answered with something that is not a valid release feed.</summary>
    FeedInvalid,

    /// <summary>The update could not be written to disk.</summary>
    Disk,

    /// <summary>A downloaded package failed its integrity check.</summary>
    VerificationFailed,

    /// <summary>The application is not installed, so it cannot update itself.</summary>
    NotInstalled,

    /// <summary>Any other failure.</summary>
    Unknown,
}
