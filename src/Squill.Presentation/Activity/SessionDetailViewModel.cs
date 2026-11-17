namespace Squill.Presentation.Activity;

/// <summary>One labelled value of the detail pane.</summary>
/// <param name="Label">Localized label.</param>
/// <param name="Value">The value as text.</param>
public sealed record DetailField(string Label, string Value);

/// <summary>The detail pane of the selected session.</summary>
public sealed class SessionDetailViewModel
{
    internal SessionDetailViewModel(SessionRowViewModel row, IReadOnlyList<DetailField> fields, string? note, bool canCancel)
    {
        Row = row;
        Fields = fields;
        Note = note;
        CanCancel = canCancel;
    }

    /// <summary>Gets the row of the session.</summary>
    public SessionRowViewModel Row { get; }

    /// <summary>Gets the fields the engine provided; the others are left out.</summary>
    public IReadOnlyList<DetailField> Fields { get; }

    /// <summary>Gets the last statement in full, or an empty text.</summary>
    public string Query => Row.Session.Query ?? "";

    /// <summary>Gets the explanation shown for an idle transaction that blocks others, built from the data; <see langword="null"/> otherwise.</summary>
    public string? Note { get; }

    /// <summary>Gets a value indicating whether "cancel the query" is offered (the engine supports it).</summary>
    public bool CanCancel { get; }
}
