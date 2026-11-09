namespace Squill.Presentation.Updates;

/// <summary>A block of parsed release notes; the heads render each kind with plain text elements.</summary>
public abstract record NotesBlock;

/// <summary>A heading line (<c>#</c> to <c>###</c> or deeper); the level is not kept, every heading is rendered alike.</summary>
/// <param name="Text">The heading text as written, never translated.</param>
public sealed record NotesHeading(string Text) : NotesBlock;

/// <summary>A paragraph made of consecutive text lines.</summary>
/// <param name="Runs">The styled pieces of the paragraph, in order.</param>
public sealed record NotesParagraph(IReadOnlyList<NotesRun> Runs) : NotesBlock;

/// <summary>A bullet item, with its indented continuation lines joined by a space.</summary>
/// <param name="Runs">The styled pieces of the item, in order.</param>
public sealed record NotesBullet(IReadOnlyList<NotesRun> Runs) : NotesBlock;

/// <summary>A piece of inline text with one style.</summary>
/// <param name="Text">The text to show.</param>
/// <param name="Style">How to show it.</param>
/// <param name="Url">The absolute http(s) target when <paramref name="Style"/> is <see cref="NotesRunStyle.Link"/>, otherwise <see langword="null"/>.</param>
public sealed record NotesRun(string Text, NotesRunStyle Style, string? Url = null);

/// <summary>The inline style of a <see cref="NotesRun"/>.</summary>
public enum NotesRunStyle
{
    /// <summary>Ordinary text.</summary>
    Plain,

    /// <summary>Bold text (<c>**bold**</c>).</summary>
    Bold,

    /// <summary>Monospaced text (<c>`code`</c>).</summary>
    Code,

    /// <summary>A link to an http(s) URL (<c>[text](https://...)</c>).</summary>
    Link,
}
