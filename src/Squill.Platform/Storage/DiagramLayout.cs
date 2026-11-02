namespace Squill.Platform.Storage;

/// <summary>User-adjusted layout of an ER diagram: box positions plus pan and zoom.</summary>
/// <param name="Positions">Box positions keyed by entity key; read back with a case-insensitive comparer.</param>
/// <param name="Zoom">Zoom factor.</param>
/// <param name="OffsetX">Horizontal pan offset.</param>
/// <param name="OffsetY">Vertical pan offset.</param>
public sealed record DiagramLayout(
    IReadOnlyDictionary<string, DiagramPosition> Positions,
    double Zoom,
    double OffsetX,
    double OffsetY);
