namespace Squill.Core.Activity;

/// <summary>Engine-neutral state of a server session.</summary>
public enum SessionState
{
    /// <summary>The session is running a statement.</summary>
    Active,

    /// <summary>The session waits for its client and holds no open transaction.</summary>
    Idle,

    /// <summary>The session waits for its client inside an open transaction.</summary>
    IdleInTransaction,

    /// <summary>The session is blocked, waiting for a lock or another resource.</summary>
    Waiting,

    /// <summary>Any other state; <see cref="ServerSession.StateText"/> keeps the engine's wording.</summary>
    Other,
}
