using Avalonia;
using System;

namespace DicomViewer.Helpers;

/// <summary>
/// Shared geometry calculations used by annotation hit-testing.
/// </summary>
public static class GeometryHelper
{
    /// <summary>
    /// Returns the shortest distance from point <paramref name="p"/>
    /// to the line segment defined by <paramref name="a"/> and <paramref name="b"/>.
    /// </summary>
    public static double DistanceToSegment(Point p, Point a, Point b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        double lenSq = dx * dx + dy * dy;
        if (lenSq < 0.001)
            return Math.Sqrt((p.X - a.X) * (p.X - a.X) + (p.Y - a.Y) * (p.Y - a.Y));
        double t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lenSq, 0, 1);
        double projX = a.X + t * dx, projY = a.Y + t * dy;
        return Math.Sqrt((p.X - projX) * (p.X - projX) + (p.Y - projY) * (p.Y - projY));
    }
}
