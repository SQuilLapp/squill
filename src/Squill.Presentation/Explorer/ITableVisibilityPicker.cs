namespace Squill.Presentation.Explorer;

/// <summary>A table or view offered in the dialog that chooses what the explorer shows.</summary>
/// <param name="Key">The table key: <c>schema.name</c>, or <c>name</c> without a schema.</param>
/// <param name="IsView">Whether it is a view, a materialized view included.</param>
/// <param name="Visible">Whether the explorer shows it now.</param>
public sealed record TableChoice(string Key, bool IsView, bool Visible);

/// <summary>The dialog, owned by a head, in which the user checks the tables and views the explorer shows.</summary>
public interface ITableVisibilityPicker
{
    /// <summary>Asks which tables and views to show.</summary>
    /// <param name="title">Dialog title, naming the database.</param>
    /// <param name="choices">Every table and view of the database, in alphabetical order.</param>
    /// <returns>The keys to show, or <see langword="null"/> when the user cancelled.</returns>
    ValueTask<IReadOnlyList<string>?> PickVisibleAsync(string title, IReadOnlyList<TableChoice> choices);
}
