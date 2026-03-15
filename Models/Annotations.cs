using Avalonia;
using Avalonia.Media;
using System;
using System.Collections.Generic;

namespace DicomViewer.Models;

/// <summary>
/// Available annotation colors — high contrast against grayscale DICOM images.
/// </summary>
public static class AnnotationColors
{
    public static readonly Color Yellow = Color.Parse("#FFD700");
    public static readonly Color Cyan = Color.Parse("#00E5FF");
    public static readonly Color Red = Color.Parse("#FF4444");
    public static readonly Color Green = Color.Parse("#44FF44");
    public static readonly Color Blue = Color.Parse("#4488FF");
    public static readonly Color Magenta = Color.Parse("#FF44FF");
    public static readonly Color Orange = Color.Parse("#FF8800");
    public static readonly Color White = Color.Parse("#FFFFFF");

    public static readonly Color[] All = { Yellow, Cyan, Red, Green, Blue, Magenta, Orange, White };
    public static readonly string[] Names = { "Yellow", "Cyan", "Red", "Green", "Blue", "Magenta", "Orange", "White" };
}

public enum AnnotationType
{
    Arrow,
    TextLabel,
    Freehand,
    Rectangle,
    Ellipse,
    Line
}

/// <summary>
/// Base class for all canvas annotations. Coordinates are in screen space.
/// </summary>
public abstract class Annotation
{
    public Guid Id { get; } = Guid.NewGuid();
    public AnnotationType Type { get; init; }
    public Color StrokeColor { get; set; } = AnnotationColors.Yellow;
    public double StrokeWidth { get; set; } = 2.0;
    public bool IsSelected { get; set; }

    public abstract Rect GetBounds();
    public abstract bool HitTest(Point point, double tolerance = 6.0);
    public abstract void Move(double dx, double dy);
}

/// <summary>
/// Arrow pointing from Tail to Head (arrowhead at Head).
/// </summary>
public class ArrowAnnotation : Annotation
{
    public Point Tail { get; set; }
    public Point Head { get; set; }

    public ArrowAnnotation() { Type = AnnotationType.Arrow; }

    public override Rect GetBounds()
    {
        double x = Math.Min(Tail.X, Head.X) - 10;
        double y = Math.Min(Tail.Y, Head.Y) - 10;
        double w = Math.Abs(Head.X - Tail.X) + 20;
        double h = Math.Abs(Head.Y - Tail.Y) + 20;
        return new Rect(x, y, w, h);
    }

    public override bool HitTest(Point p, double tolerance)
    {
        return DistanceToSegment(p, Tail, Head) <= tolerance;
    }

    public override void Move(double dx, double dy)
    {
        Tail = new Point(Tail.X + dx, Tail.Y + dy);
        Head = new Point(Head.X + dx, Head.Y + dy);
    }

    private static double DistanceToSegment(Point p, Point a, Point b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        double lenSq = dx * dx + dy * dy;
        if (lenSq < 0.001) return Math.Sqrt((p.X - a.X) * (p.X - a.X) + (p.Y - a.Y) * (p.Y - a.Y));
        double t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lenSq, 0, 1);
        double projX = a.X + t * dx, projY = a.Y + t * dy;
        return Math.Sqrt((p.X - projX) * (p.X - projX) + (p.Y - projY) * (p.Y - projY));
    }
}

/// <summary>
/// Text label placed at a position on the canvas.
/// </summary>
public class TextAnnotation : Annotation
{
    public Point Position { get; set; }
    public string Text { get; set; } = "Label";
    public double FontSize { get; set; } = 14;

    public TextAnnotation() { Type = AnnotationType.TextLabel; }

    public override Rect GetBounds()
    {
        double w = Math.Max(40, Text.Length * FontSize * 0.6);
        return new Rect(Position.X - 2, Position.Y - 2, w + 6, FontSize + 8);
    }

    public override bool HitTest(Point p, double tolerance)
    {
        var b = GetBounds();
        return b.Inflate(tolerance).Contains(p);
    }

    public override void Move(double dx, double dy)
    {
        Position = new Point(Position.X + dx, Position.Y + dy);
    }
}

/// <summary>
/// Freehand drawing stroke (list of points).
/// </summary>
public class FreehandAnnotation : Annotation
{
    public List<Point> Points { get; set; } = new();

    public FreehandAnnotation() { Type = AnnotationType.Freehand; }

    public override Rect GetBounds()
    {
        if (Points.Count == 0) return default;
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        foreach (var p in Points)
        {
            if (p.X < minX) minX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.X > maxX) maxX = p.X;
            if (p.Y > maxY) maxY = p.Y;
        }
        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    public override bool HitTest(Point pt, double tolerance)
    {
        for (int i = 1; i < Points.Count; i++)
        {
            if (DistanceToSegment(pt, Points[i - 1], Points[i]) <= tolerance)
                return true;
        }
        return false;
    }

    public override void Move(double dx, double dy)
    {
        for (int i = 0; i < Points.Count; i++)
            Points[i] = new Point(Points[i].X + dx, Points[i].Y + dy);
    }

    private static double DistanceToSegment(Point p, Point a, Point b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        double lenSq = dx * dx + dy * dy;
        if (lenSq < 0.001) return Math.Sqrt((p.X - a.X) * (p.X - a.X) + (p.Y - a.Y) * (p.Y - a.Y));
        double t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lenSq, 0, 1);
        double projX = a.X + t * dx, projY = a.Y + t * dy;
        return Math.Sqrt((p.X - projX) * (p.X - projX) + (p.Y - projY) * (p.Y - projY));
    }
}

/// <summary>
/// Rectangle annotation defined by two corner points.
/// </summary>
public class RectangleAnnotation : Annotation
{
    public Point TopLeft { get; set; }
    public Point BottomRight { get; set; }

    public RectangleAnnotation() { Type = AnnotationType.Rectangle; }

    public Rect GetRect()
    {
        double x = Math.Min(TopLeft.X, BottomRight.X);
        double y = Math.Min(TopLeft.Y, BottomRight.Y);
        double w = Math.Abs(BottomRight.X - TopLeft.X);
        double h = Math.Abs(BottomRight.Y - TopLeft.Y);
        return new Rect(x, y, w, h);
    }

    public override Rect GetBounds() => GetRect().Inflate(5);

    public override bool HitTest(Point p, double tolerance)
    {
        var r = GetRect();
        var outer = r.Inflate(tolerance);
        var inner = r.Inflate(-tolerance);
        return outer.Contains(p) && (inner.Width <= 0 || inner.Height <= 0 || !inner.Contains(p));
    }

    public override void Move(double dx, double dy)
    {
        TopLeft = new Point(TopLeft.X + dx, TopLeft.Y + dy);
        BottomRight = new Point(BottomRight.X + dx, BottomRight.Y + dy);
    }
}

/// <summary>
/// Ellipse annotation defined by a bounding box.
/// </summary>
public class EllipseAnnotation : Annotation
{
    public Point TopLeft { get; set; }
    public Point BottomRight { get; set; }

    public EllipseAnnotation() { Type = AnnotationType.Ellipse; }

    public Rect GetRect()
    {
        double x = Math.Min(TopLeft.X, BottomRight.X);
        double y = Math.Min(TopLeft.Y, BottomRight.Y);
        double w = Math.Abs(BottomRight.X - TopLeft.X);
        double h = Math.Abs(BottomRight.Y - TopLeft.Y);
        return new Rect(x, y, w, h);
    }

    public Point Center
    {
        get
        {
            var r = GetRect();
            return new Point(r.X + r.Width / 2, r.Y + r.Height / 2);
        }
    }

    public override Rect GetBounds() => GetRect().Inflate(5);

    public override bool HitTest(Point p, double tolerance)
    {
        var r = GetRect();
        if (r.Width < 1 || r.Height < 1) return false;
        double cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
        double rx = r.Width / 2, ry = r.Height / 2;
        double dx = p.X - cx, dy = p.Y - cy;
        double outer = (dx * dx) / ((rx + tolerance) * (rx + tolerance)) + (dy * dy) / ((ry + tolerance) * (ry + tolerance));
        double inner = rx > tolerance && ry > tolerance
            ? (dx * dx) / ((rx - tolerance) * (rx - tolerance)) + (dy * dy) / ((ry - tolerance) * (ry - tolerance))
            : 0;
        return outer <= 1.0 && inner >= 1.0;
    }

    public override void Move(double dx, double dy)
    {
        TopLeft = new Point(TopLeft.X + dx, TopLeft.Y + dy);
        BottomRight = new Point(BottomRight.X + dx, BottomRight.Y + dy);
    }
}

/// <summary>
/// Simple line segment annotation.
/// </summary>
public class LineAnnotation : Annotation
{
    public Point Start { get; set; }
    public Point End { get; set; }

    public LineAnnotation() { Type = AnnotationType.Line; }

    public override Rect GetBounds()
    {
        double x = Math.Min(Start.X, End.X) - 5;
        double y = Math.Min(Start.Y, End.Y) - 5;
        return new Rect(x, y, Math.Abs(End.X - Start.X) + 10, Math.Abs(End.Y - Start.Y) + 10);
    }

    public override bool HitTest(Point p, double tolerance)
    {
        return DistanceToSegment(p, Start, End) <= tolerance;
    }

    public override void Move(double dx, double dy)
    {
        Start = new Point(Start.X + dx, Start.Y + dy);
        End = new Point(End.X + dx, End.Y + dy);
    }

    private static double DistanceToSegment(Point p, Point a, Point b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        double lenSq = dx * dx + dy * dy;
        if (lenSq < 0.001) return Math.Sqrt((p.X - a.X) * (p.X - a.X) + (p.Y - a.Y) * (p.Y - a.Y));
        double t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lenSq, 0, 1);
        double projX = a.X + t * dx, projY = a.Y + t * dy;
        return Math.Sqrt((p.X - projX) * (p.X - projX) + (p.Y - projY) * (p.Y - projY));
    }
}
