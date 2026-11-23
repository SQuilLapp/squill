namespace Squill.Presentation.Explorer;

/// <summary>The size of the explorer filter popup and the rules both heads share to keep it inside the window.</summary>
/// <param name="Width">The width in device-independent pixels.</param>
/// <param name="Height">The height in device-independent pixels.</param>
public readonly record struct ExplorerFilterPopupSize(double Width, double Height)
{
    /// <summary>The gap kept between the popup and the window edge.</summary>
    public const double WindowMargin = 8;

    /// <summary>Gets the size of a popup the user has not resized.</summary>
    public static ExplorerFilterPopupSize Default { get; } = new(440, 520);

    /// <summary>Gets the smallest size the user can drag the popup to.</summary>
    public static ExplorerFilterPopupSize Minimum { get; } = new(320, 220);

    /// <summary>Clamps a wanted size between <see cref="Minimum"/> and the available space; a space smaller than the minimum wins.</summary>
    /// <param name="wanted">The size the user or the last session asked for.</param>
    /// <param name="maxWidth">The width available in the window.</param>
    /// <param name="maxHeight">The height available in the window.</param>
    /// <returns>The size to apply.</returns>
    public static ExplorerFilterPopupSize Fit(ExplorerFilterPopupSize wanted, double maxWidth, double maxHeight) =>
        new(Side(wanted.Width, Minimum.Width, maxWidth), Side(wanted.Height, Minimum.Height, maxHeight));

    /// <summary>Tells whether the popup should open above its anchor.</summary>
    /// <param name="height">The wanted popup height.</param>
    /// <param name="spaceBelow">The room under the anchor.</param>
    /// <param name="spaceAbove">The room over the anchor.</param>
    /// <returns><see langword="true"/> when it does not fit below and there is more room above.</returns>
    public static bool OpensAbove(double height, double spaceBelow, double spaceAbove) => height > spaceBelow && spaceAbove > spaceBelow;

    private static double Side(double value, double min, double max)
    {
        max = Math.Max(max, 0);
        return Math.Max(Math.Min(value, max), Math.Min(min, max));
    }
}
