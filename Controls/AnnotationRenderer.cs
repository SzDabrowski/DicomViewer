using Avalonia;
using Avalonia.Media;
using DicomViewer.Models;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace DicomViewer.Controls;

/// <summary>
/// Responsible for drawing annotations onto a DrawingContext.
/// Extracted from DicomCanvas to isolate rendering logic from interaction.
/// </summary>
public static class AnnotationRenderer
{
    private static readonly Typeface LabelFont = new(FontFamily.Default);

    public static void Render(DrawingContext ctx, Annotation ann, bool isPreview, bool isEditingText, TextAnnotation? editingAnnotation)
    {
        var brush = new SolidColorBrush(ann.StrokeColor);
        var pen = new Pen(brush, ann.StrokeWidth);
        var dashPen = isPreview ? new Pen(brush, ann.StrokeWidth, new DashStyle(new double[] { 4, 3 }, 0)) : pen;
        var usePen = isPreview ? dashPen : pen;

        switch (ann)
        {
            case ArrowAnnotation arrow:
                DrawArrow(ctx, arrow.Tail, arrow.Head, usePen, brush);
                break;

            case TextAnnotation text:
                DrawTextLabel(ctx, text, brush, isEditingText, editingAnnotation);
                break;

            case FreehandAnnotation freehand:
                DrawFreehand(ctx, freehand.Points, usePen);
                break;

            case RectangleAnnotation rect:
                ctx.DrawRectangle(null, usePen, rect.GetRect());
                break;

            case EllipseAnnotation ellipse:
                var er = ellipse.GetRect();
                ctx.DrawEllipse(null, usePen, er.Center, er.Width / 2, er.Height / 2);
                break;

            case LineAnnotation line:
                ctx.DrawLine(usePen, line.Start, line.End);
                break;
        }

        // Selection handles
        if (ann.IsSelected && !isPreview)
        {
            var b = ann.GetBounds();
            var selPen = new Pen(Brushes.White, 1, new DashStyle(new double[] { 3, 3 }, 0));
            ctx.DrawRectangle(null, selPen, b);
        }
    }

    private static void DrawArrow(DrawingContext ctx, Point tail, Point head, IPen pen, IBrush brush)
    {
        ctx.DrawLine(pen, tail, head);

        double dx = head.X - tail.X, dy = head.Y - tail.Y;
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 3) return;
        double nx = dx / len, ny = dy / len;
        double arrowSize = Math.Min(12, len * 0.3);

        var p1 = new Point(head.X - arrowSize * (nx - ny * 0.4),
                           head.Y - arrowSize * (ny + nx * 0.4));
        var p2 = new Point(head.X - arrowSize * (nx + ny * 0.4),
                           head.Y - arrowSize * (ny - nx * 0.4));

        var geo = new StreamGeometry();
        using (var gc = geo.Open())
        {
            gc.BeginFigure(head, true);
            gc.LineTo(p1);
            gc.LineTo(p2);
            gc.EndFigure(true);
        }
        ctx.DrawGeometry(brush, null, geo);
    }

    private static void DrawTextLabel(DrawingContext ctx, TextAnnotation text, IBrush brush, bool isEditingText, TextAnnotation? editingAnnotation)
    {
        var displayText = text.Text;
        if (isEditingText && editingAnnotation == text)
            displayText += "|"; // cursor

        var ft = new FormattedText(displayText,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, LabelFont, text.FontSize, brush);

        ctx.FillRectangle(new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)),
            new Rect(text.Position.X - 3, text.Position.Y - 2, ft.Width + 8, ft.Height + 4));
        ctx.DrawText(ft, text.Position);
    }

    private static void DrawFreehand(DrawingContext ctx, List<Point> points, IPen pen)
    {
        if (points.Count < 2) return;

        var geo = new StreamGeometry();
        using (var gc = geo.Open())
        {
            gc.BeginFigure(points[0], false);
            for (int i = 1; i < points.Count; i++)
                gc.LineTo(points[i]);
            gc.EndFigure(false);
        }
        ctx.DrawGeometry(null, pen, geo);
    }
}
