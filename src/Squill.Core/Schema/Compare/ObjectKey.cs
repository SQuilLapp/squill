namespace Squill.Core.Schema.Compare;

/// <summary>Identifies one <see cref="ObjectDifference"/>, for example in the set of objects a migration script includes. Names compare case-insensitively.</summary>
/// <param name="Category">Group of the object.</param>
/// <param name="Schema">Schema of the object; null when the engine has none.</param>
/// <param name="Name">Name as in <see cref="ObjectDifference.Name"/>.</param>
public readonly record struct ObjectKey(ObjectCategory Category, string? Schema, string Name)
{
    /// <summary>Creates the key of a difference.</summary>
    /// <param name="difference">The difference.</param>
    /// <returns>The key.</returns>
    public static ObjectKey Of(ObjectDifference difference)
    {
        ArgumentNullException.ThrowIfNull(difference);
        return new(difference.Category, difference.Schema, difference.Name);
    }

    /// <inheritdoc/>
    public bool Equals(ObjectKey other) =>
        Category == other.Category
        && string.Equals(Schema ?? "", other.Schema ?? "", StringComparison.OrdinalIgnoreCase)
        && string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public override int GetHashCode() =>
        HashCode.Combine(Category, StringComparer.OrdinalIgnoreCase.GetHashCode(Schema ?? ""), StringComparer.OrdinalIgnoreCase.GetHashCode(Name));
}
