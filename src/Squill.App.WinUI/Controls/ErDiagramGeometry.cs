using System.Globalization;
using Squill.Presentation.Diagram;

namespace Squill.App.WinUI.Controls;

/// <summary>Pure geometry and text rules of the ER diagram chrome, kept free of UI types so they can be tested.</summary>
internal static class ErDiagramGeometry
{
    /// <summary>Tells whether a point lies within a tolerance of a polyline edge.</summary>
    /// <param name="edge">The routed edge.</param>
    /// <param name="p">The point, in world coordinates.</param>
    /// <param name="tolerance">The maximum distance in world units.</param>
    /// <returns><see langword="true"/> when some segment of the edge passes within the tolerance.</returns>
    public static bool Near(ErEdge edge, ErPoint p, double tolerance)
    {
        for (var i = 1; i < edge.Points.Count; i++)
        {
            double ax = edge.Points[i - 1].X, ay = edge.Points[i - 1].Y;
            double dx = edge.Points[i].X - ax, dy = edge.Points[i].Y - ay;
            var len2 = (dx * dx) + (dy * dy);
            var t = len2 < 1e-9 ? 0 : Math.Clamp((((p.X - ax) * dx) + ((p.Y - ay) * dy)) / len2, 0, 1);
            double ex = p.X - (ax + (t * dx)), ey = p.Y - (ay + (t * dy));
            if ((ex * ex) + (ey * ey) <= tolerance * tolerance)
                return true;
        }

        return false;
    }

    /// <summary>Formats a zoom factor as drawn in the toolbar.</summary>
    /// <param name="zoom">The zoom factor, 1 meaning 100 %.</param>
    /// <returns>For example <c>85 %</c>.</returns>
    public static string ZoomText(double zoom) => Math.Round(zoom * 100).ToString(CultureInfo.InvariantCulture) + " %";
}
