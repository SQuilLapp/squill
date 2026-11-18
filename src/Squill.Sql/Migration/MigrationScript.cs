namespace Squill.Sql.Migration;

/// <summary>A fact about a generated migration script that the user should see; the UI maps each kind to a translated text.</summary>
public enum MigrationNoteKind
{
    /// <summary>The script drops a table or a column and the data in it.</summary>
    DropsData,

    /// <summary>A column type changes in a way that can lose data (another type, or a smaller length or precision).</summary>
    NarrowsColumn,

    /// <summary>MySQL commits DDL implicitly, so the script cannot be rolled back.</summary>
    MySqlImplicitCommit,

    /// <summary>SQLite rebuilds the table to apply an alteration; its foreign-key checks are switched off around the transaction.</summary>
    SqliteRebuild,
}

/// <summary>One note about a migration script.</summary>
/// <param name="Kind">What the note says.</param>
/// <param name="Subject">The object concerned (<c>table</c> or <c>table.column</c>); empty when the note is about the whole script.</param>
public sealed record MigrationNote(MigrationNoteKind Kind, string Subject);

/// <summary>A generated migration script.</summary>
/// <param name="Text">The SQL text, comments included.</param>
/// <param name="ChangeCount">Number of included differences the script applies.</param>
/// <param name="Notes">Facts to show next to the script.</param>
public sealed record MigrationScript(string Text, int ChangeCount, IReadOnlyList<MigrationNote> Notes);

/// <summary>Inputs of <see cref="MigrationScriptBuilder.Build"/> that are not schema data.</summary>
/// <param name="SourceName">Display name of the source connection, written in the header comment.</param>
/// <param name="TargetName">Display name of the target connection, written in the header comment.</param>
/// <param name="GeneratedAt">Local date and time written in the header comment; the builder never reads the clock.</param>
public sealed record MigrationOptions(string SourceName, string TargetName, DateTime GeneratedAt)
{
    /// <summary>Whether the statements are wrapped in a transaction (ignored for MySQL, which has none for DDL).</summary>
    public bool WrapInTransaction { get; init; } = true;
}
