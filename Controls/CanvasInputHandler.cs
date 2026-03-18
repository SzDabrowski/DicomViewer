using Avalonia;
using Avalonia.Input;
using DicomViewer.Constants;
using DicomViewer.Models;
using DicomViewer.ViewModels;
using System;
using System.Collections.Generic;

namespace DicomViewer.Controls;

/// <summary>
/// Handles mouse/keyboard interaction for DicomCanvas.
/// Extracted from DicomCanvas to isolate input logic from rendering.
/// </summary>
public class CanvasInputHandler
{
    private readonly DicomCanvas _canvas;

    // Interaction state
    private bool _isDragging;
    private Point _lastPointerPos;
    private Point _dragStart;

    // In-progress annotation being drawn
    private Annotation? _activeAnnotation;

    // Text editing state
    private bool _isEditingText;
    private TextAnnotation? _editingTextAnnotation;

    public bool IsEditingText => _isEditingText;
    public TextAnnotation? EditingTextAnnotation => _editingTextAnnotation;
    public Annotation? ActiveAnnotation => _activeAnnotation;

    public CanvasInputHandler(DicomCanvas canvas)
    {
        _canvas = canvas;
    }

    public void HandlePointerPressed(PointerPressedEventArgs e)
    {
        var pos = e.GetPosition(_canvas);
        _lastPointerPos = pos;
        _dragStart = pos;
        _isDragging = true;

        if (_canvas.ActiveTool == MouseTool.None) return;

        if (_canvas.ActiveTool == MouseTool.Pan)
            _canvas.UpdateCursorPublic(dragging: true);

        if (IsAnnotationTool)
        {
            FinishTextEditing();
            _activeAnnotation = _canvas.ActiveTool switch
            {
                MouseTool.Arrow => new ArrowAnnotation
                {
                    Tail = pos, Head = pos,
                    StrokeColor = _canvas.CurrentColorPublic, StrokeWidth = _canvas.AnnotationStrokeWidth
                },
                MouseTool.TextLabel => null,
                MouseTool.Freehand => new FreehandAnnotation
                {
                    Points = new List<Point> { pos },
                    StrokeColor = _canvas.CurrentColorPublic, StrokeWidth = _canvas.AnnotationStrokeWidth
                },
                MouseTool.DrawRect => new RectangleAnnotation
                {
                    TopLeft = pos, BottomRight = pos,
                    StrokeColor = _canvas.CurrentColorPublic, StrokeWidth = _canvas.AnnotationStrokeWidth
                },
                MouseTool.DrawEllipse => new EllipseAnnotation
                {
                    TopLeft = pos, BottomRight = pos,
                    StrokeColor = _canvas.CurrentColorPublic, StrokeWidth = _canvas.AnnotationStrokeWidth
                },
                MouseTool.DrawLine => new LineAnnotation
                {
                    Start = pos, End = pos,
                    StrokeColor = _canvas.CurrentColorPublic, StrokeWidth = _canvas.AnnotationStrokeWidth
                },
                _ => null
            };
        }
    }

    public void HandlePointerReleased(PointerReleasedEventArgs e)
    {
        var pos = e.GetPosition(_canvas);

        if (_activeAnnotation != null)
        {
            double dragDist = Math.Sqrt(Math.Pow(pos.X - _dragStart.X, 2) + Math.Pow(pos.Y - _dragStart.Y, 2));
            if (dragDist > UIConstants.AnnotationDragThreshold)
            {
                _canvas.AddAnnotation(_activeAnnotation);
            }
            _activeAnnotation = null;
            _canvas.InvalidateVisual();
        }
        else if (_canvas.ActiveTool == MouseTool.TextLabel && _isDragging)
        {
            var textAnn = new TextAnnotation
            {
                Position = pos,
                Text = "",
                FontSize = _canvas.AnnotationFontSize,
                StrokeColor = _canvas.CurrentColorPublic, StrokeWidth = _canvas.AnnotationStrokeWidth
            };
            _canvas.AddAnnotation(textAnn);
            _isEditingText = true;
            _editingTextAnnotation = textAnn;
            _canvas.InvalidateVisual();
        }

        _isDragging = false;
        if (_canvas.ActiveTool == MouseTool.Pan)
            _canvas.UpdateCursorPublic(dragging: false);
    }

    public void HandlePointerMoved(PointerEventArgs e)
    {
        if (!_isDragging) return;

        var pos = e.GetPosition(_canvas);
        double dx = pos.X - _lastPointerPos.X;
        double dy = pos.Y - _lastPointerPos.Y;

        switch (_canvas.ActiveTool)
        {
            case MouseTool.Pan:
                _canvas.PanX += dx;
                _canvas.PanY += dy;
                _canvas.RaisePanChanged();
                break;

            case MouseTool.WindowLevel:
                _canvas.WindowCenter += dx * 2.0;
                _canvas.WindowWidth = Math.Max(1, _canvas.WindowWidth + dy * 4.0);
                _canvas.RaiseWindowLevelChanged();
                break;
        }

        if (_activeAnnotation != null)
        {
            switch (_activeAnnotation)
            {
                case ArrowAnnotation arrow:
                    arrow.Head = pos;
                    break;
                case FreehandAnnotation freehand:
                    freehand.Points.Add(pos);
                    break;
                case RectangleAnnotation rect:
                    rect.BottomRight = pos;
                    break;
                case EllipseAnnotation ellipse:
                    ellipse.BottomRight = pos;
                    break;
                case LineAnnotation line:
                    line.End = pos;
                    break;
            }
        }

        _lastPointerPos = pos;
        _canvas.InvalidateVisual();
    }

    public void HandlePointerWheelChanged(PointerWheelEventArgs e)
    {
        if (_canvas.ActiveTool == MouseTool.Pan)
        {
            double delta = e.Delta.Y > 0 ? 1.12 : 0.89;
            var pos = e.GetPosition(_canvas);
            var bounds = _canvas.Bounds;
            _canvas.PanX = pos.X - (pos.X - (bounds.Width / 2 + _canvas.PanX)) * delta - bounds.Width / 2;
            _canvas.PanY = pos.Y - (pos.Y - (bounds.Height / 2 + _canvas.PanY)) * delta - bounds.Height / 2;
            _canvas.ZoomLevel = Math.Clamp(_canvas.ZoomLevel * delta, 0.05, 20.0);
            _canvas.RaiseZoomLevelChanged();
            _canvas.RaisePanChanged();
        }
        else
        {
            int direction = e.Delta.Y > 0 ? -1 : 1;
            _canvas.RaiseFrameScrolled(direction);
        }

        _canvas.InvalidateVisual();
    }

    public void HandleTextInput(TextInputEventArgs e)
    {
        if (_isEditingText && _editingTextAnnotation != null && !string.IsNullOrEmpty(e.Text))
        {
            _editingTextAnnotation.Text += e.Text;
            _canvas.InvalidateVisual();
            e.Handled = true;
        }
    }

    public bool HandleKeyDown(KeyEventArgs e)
    {
        if (_isEditingText && _editingTextAnnotation != null)
        {
            switch (e.Key)
            {
                case Key.Back:
                    if (_editingTextAnnotation.Text.Length > 0)
                        _editingTextAnnotation.Text = _editingTextAnnotation.Text[..^1];
                    _canvas.InvalidateVisual();
                    return true;
                case Key.Enter:
                case Key.Escape:
                    FinishTextEditing();
                    return true;
            }
            if (e.Key != Key.LeftCtrl && e.Key != Key.RightCtrl)
                return true;
            return true;
        }

        return false;
    }

    public void FinishTextEditing()
    {
        if (!_isEditingText || _editingTextAnnotation == null) return;

        if (string.IsNullOrWhiteSpace(_editingTextAnnotation.Text))
            _canvas.RemoveAnnotation(_editingTextAnnotation);

        _isEditingText = false;
        _editingTextAnnotation = null;
        _canvas.InvalidateVisual();
    }

    private bool IsAnnotationTool => _canvas.ActiveTool is MouseTool.Arrow or MouseTool.TextLabel
        or MouseTool.Freehand or MouseTool.DrawRect or MouseTool.DrawEllipse or MouseTool.DrawLine;
}
