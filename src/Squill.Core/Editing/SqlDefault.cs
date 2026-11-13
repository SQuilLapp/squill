namespace Squill.Core.Editing;

/// <summary>The value of a cell the user left to the column default: the SQL keyword <c>DEFAULT</c>, never a bound parameter.</summary>
public sealed class SqlDefault
{
    private SqlDefault()
    {
    }

    /// <summary>Gets the one instance.</summary>
    public static SqlDefault Value { get; } = new();
}
