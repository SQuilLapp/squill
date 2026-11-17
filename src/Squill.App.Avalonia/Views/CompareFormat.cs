using Squill.Core;
using Squill.Presentation.Compare;

namespace Squill.App.Avalonia.Views;

/// <summary>How a row of one DDL pane is drawn.</summary>
internal enum DdlRowKind
{
    /// <summary>A line both sides share: no tint, no mark.</summary>
    Same,

    /// <summary>A line the target lacks (source side): green tint and <c>+</c>.</summary>
    Added,

    /// <summary>A line only the target has, or the target version of a changed line: red tint and <c>-</c>.</summary>
    Removed,

    /// <summary>No line on this side: hatched, no number.</summary>
    Filler,
}

/// <summary>Texts and row classification of the schema compare view that need no UI thread.</summary>
internal static class CompareFormat
{
    /// <summary>Reads what one pane shows for an aligned row.</summary>
    /// <param name="line">The aligned row.</param>
    /// <param name="sourceSide"><see langword="true"/> for the left (source) pane.</param>
    /// <returns>The line number and text (<see langword="null"/> for a filler) and how the row is drawn.</returns>
    public static (int? Number, string? Text, DdlRowKind Kind) Row(AlignedLine line, bool sourceSide)
    {
        var (number, text) = sourceSide ? (line.SourceNumber, line.SourceText) : (line.TargetNumber, line.TargetText);
        if (text is null)
            return (null, null, DdlRowKind.Filler);
        return (number, text, line.Mark == LineMark.Same ? DdlRowKind.Same : sourceSide ? DdlRowKind.Added : DdlRowKind.Removed);
    }

    /// <summary>Gets the mark drawn between the line number and the text.</summary>
    /// <param name="kind">The row kind.</param>
    /// <returns><c>+</c>, <c>-</c> or an empty string.</returns>
    public static string Mark(DdlRowKind kind) => kind switch { DdlRowKind.Added => "+", DdlRowKind.Removed => "-", _ => "" };

    /// <summary>Gets the two letters of the engine badge of an endpoint chip.</summary>
    /// <param name="dialect">The engine of the connection.</param>
    /// <returns>The badge letters.</returns>
    public static string EngineLetters(DatabaseDialect dialect) => dialect switch
    {
        DatabaseDialect.PostgreSql => "PG",
        DatabaseDialect.MySql => "MY",
        DatabaseDialect.SqlServer => "MS",
        DatabaseDialect.Sqlite => "SL",
        DatabaseDialect.Oracle => "OR",
        _ => "DB",
    };

    /// <summary>Gets the token suffix (<c>SquillEnv{suffix}Brush</c>) and the status text key of an environment.</summary>
    /// <param name="environment">The environment of a connection.</param>
    /// <returns>The suffix and the localization key, or <see langword="null"/> for no environment.</returns>
    public static (string Suffix, string Key)? Environment(ConnectionEnvironment environment) => environment switch
    {
        ConnectionEnvironment.Production => ("Prod", "status.production"),
        ConnectionEnvironment.Staging => ("Staging", "status.staging"),
        ConnectionEnvironment.Development => ("Dev", "status.dev"),
        ConnectionEnvironment.Local => ("Local", "status.local"),
        _ => null,
    };
}
