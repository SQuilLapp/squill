namespace Squill.Platform.Updates;

/// <summary>A failure of an <see cref="IUpdateEngine"/>, already mapped to an <see cref="UpdateFailure"/>.</summary>
public sealed class UpdateEngineException : Exception
{
    /// <summary>Creates the exception.</summary>
    /// <param name="kind">What kind of failure it is.</param>
    /// <param name="message">What failed, for the logs and the details view.</param>
    public UpdateEngineException(UpdateFailure kind, string message) : base(message) => Kind = kind;

    /// <summary>Creates the exception with the failure that caused it.</summary>
    /// <param name="kind">What kind of failure it is.</param>
    /// <param name="message">What failed.</param>
    /// <param name="innerException">The underlying failure.</param>
    public UpdateEngineException(UpdateFailure kind, string message, Exception? innerException) : base(message, innerException) => Kind = kind;

    /// <summary>Gets the kind of failure.</summary>
    public UpdateFailure Kind { get; }
}
