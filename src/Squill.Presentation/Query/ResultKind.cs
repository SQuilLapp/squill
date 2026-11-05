namespace Squill.Presentation.Query;

/// <summary>What a <see cref="ResultViewModel"/> shows.</summary>
public enum ResultKind
{
    /// <summary>A grid of rows.</summary>
    Grid,

    /// <summary>A text message: rows affected, completion or an error.</summary>
    Message,

    /// <summary>The server messages of a statement (<c>PRINT</c>, <c>RAISE NOTICE</c>, warnings), one per line.</summary>
    Messages,
}
