using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using DicomViewer.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace DicomViewer.Controls
{
    public enum MouseTool { None, Pan, WindowLevel, Measure, Annotate, Rotate }

    public struct MeasurementData
    {
        public Point Start { get; set; }
        public Point End { get; set; }
        public string Value { get; set; }
    }

    public class DicomCanvas : Control
    {
        // View-Model synchronization events for interactive tools
        public event EventHandler<double>? ZoomLevelChanged;
        public event EventHandler<(double X, double Y)>? PanChanged;
        public event EventHandler<(double Center, double Width)>? WindowLevelChanged;
        public event EventHandler<int>? FrameScrolled;

        // Frame-persistent dictionary to store measurements per frame
        private readonly Dictionary<int, List<MeasurementData>> _persistentMeasurements = new();

        // Styled Properties for XAML Binding
        public static readonly StyledProperty<int> CurrentFrameIndexProperty = AvaloniaProperty.Register<DicomCanvas, int>(nameof(CurrentFrameIndex), 0);
        public static readonly StyledProperty<DicomMetadata?> MetadataProperty = AvaloniaProperty.Register<DicomCanvas, DicomMetadata?>(nameof(Metadata));
        public static readonly StyledProperty<double> WindowWidthProperty = AvaloniaProperty.Register<DicomCanvas, double>(nameof(WindowWidth), 65000);
        public static readonly StyledProperty<double> WindowCenterProperty = AvaloniaProperty.Register<DicomCanvas, double>(nameof(WindowCenter), 32000);
        public static readonly StyledProperty<double> ZoomLevelProperty = AvaloniaProperty.Register<DicomCanvas, double>(nameof(ZoomLevel), 1.0);
        public static readonly StyledProperty<double> PanXProperty = AvaloniaProperty.Register<DicomCanvas, double>(nameof(PanX), 0.0);
        public static readonly StyledProperty<double> PanYProperty = AvaloniaProperty.Register<DicomCanvas, double>(nameof(PanY), 0.0);
        public static readonly StyledProperty<MouseTool> ActiveToolProperty = AvaloniaProperty.Register<DicomCanvas, MouseTool>(nameof(ActiveTool), MouseTool.Pan);
        public static readonly StyledProperty<bool> IsInvertedProperty = AvaloniaProperty.Register<DicomCanvas, bool>(nameof(IsInverted), false);
        public static readonly StyledProperty<double> RotationProperty = AvaloniaProperty.Register<DicomCanvas, double>(nameof(Rotation), 0.0);
        public static readonly StyledProperty<bool> IsFlippedHProperty = AvaloniaProperty.Register<DicomCanvas, bool>(nameof(IsFlippedH), false);
        public static readonly StyledProperty<bool> IsFlippedVProperty = AvaloniaProperty.Register<DicomCanvas, bool>(nameof(IsFlippedV), false);

        public int CurrentFrameIndex { get => GetValue(CurrentFrameIndexProperty); set => SetValue(CurrentFrameIndexProperty, value); }
        public DicomMetadata? Metadata { get => GetValue(MetadataProperty); set => SetValue(MetadataProperty, value); }
        public double WindowWidth { get => GetValue(WindowWidthProperty); set => SetValue(WindowWidthProperty, value); }
        public double WindowCenter { get => GetValue(WindowCenterProperty); set => SetValue(WindowCenterProperty, value); }
        public double ZoomLevel { get => GetValue(ZoomLevelProperty); set => SetValue(ZoomLevelProperty, value); }
        public double PanX { get => GetValue(PanXProperty); set => SetValue(PanXProperty, value); }
        public double PanY { get => GetValue(PanYProperty); set => SetValue(PanYProperty, value); }
        public MouseTool ActiveTool { get => GetValue(ActiveToolProperty); set => SetValue(ActiveToolProperty, value); }
        public bool IsInverted { get => GetValue(IsInvertedProperty); set => SetValue(IsInvertedProperty, value); }
        public double Rotation { get => GetValue(RotationProperty); set => SetValue(RotationProperty, value); }
        public bool IsFlippedH { get => GetValue(IsFlippedHProperty); set => SetValue(IsFlippedHProperty, value); }
        public bool IsFlippedV { get => GetValue(IsFlippedVProperty); set => SetValue(IsFlippedVProperty, value); }

        private ushort[]? _pixels;
        private int _imgWidth, _imgHeight;
        private WriteableBitmap? _bitmap;
        private bool _isDragging;
        private Point _lastPointerPos;
        private Point? _measureStart;

        static DicomCanvas() { AffectsRender<DicomCanvas>(CurrentFrameIndexProperty, WindowWidthProperty, WindowCenterProperty, ZoomLevelProperty, PanXProperty, PanYProperty, MetadataProperty, IsInvertedProperty, RotationProperty, IsFlippedHProperty, IsFlippedVProperty); }

        public DicomCanvas() { ClipToBounds = true; Focusable = true; }

        public void ClearAllData() { _persistentMeasurements.Clear(); InvalidateVisual(); }

        public void SetPixels(ushort[] pixels, int width, int height)
        {
            _pixels = pixels; _imgWidth = width; _imgHeight = height;
            RebuildBitmap(); InvalidateVisual();
        }

        // --- COORDINATE MAPPING (Required for Zoom/Pan persistent measurements) ---
        private Point ScreenToImage(Point screenPoint)
        {
            if (_imgWidth <= 0 || _imgHeight <= 0) return screenPoint;
            var bounds = Bounds;
            double cx = bounds.Width / 2 + PanX;
            double cy = bounds.Height / 2 + PanY;
            double scale = Math.Min(bounds.Width / _imgWidth, bounds.Height / _imgHeight) * ZoomLevel;
            return new Point((screenPoint.X - (cx - (_imgWidth * scale) / 2)) / scale, (screenPoint.Y - (cy - (_imgHeight * scale) / 2)) / scale);
        }

        private Point ImageToScreen(Point imgPoint)
        {
            if (_imgWidth <= 0 || _imgHeight <= 0) return imgPoint;
            var bounds = Bounds;
            double cx = bounds.Width / 2 + PanX;
            double cy = bounds.Height / 2 + PanY;
            double scale = Math.Min(bounds.Width / _imgWidth, bounds.Height / _imgHeight) * ZoomLevel;
            return new Point((imgPoint.X * scale) + (cx - (_imgWidth * scale) / 2), (imgPoint.Y * scale) + (cy - (_imgHeight * scale) / 2));
        }

        // Medical calculation logic supporting US Regions and Pixel Spacing
        private string CalculatePhysicalDistance(Point p1, Point p2)
        {
            if (Metadata == null) return "No Scale";
            var region = Metadata.USRegions?.FirstOrDefault(r => p1.X >= r.X0 && p1.X <= r.X1 && p1.Y >= r.Y0 && p1.Y <= r.Y1);
            if (region != null && region.Units == 3)
            { // 3 = CM
                double dx = (p2.X - p1.X) * region.DeltaX;
                double dy = (p2.Y - p1.Y) * region.DeltaY;
                return $"{(Math.Sqrt(dx * dx + dy * dy) * 10):F1} mm";
            }
            if (Metadata.PixelSpacingX.HasValue)
            {
                double dx = (p2.X - p1.X) * Metadata.PixelSpacingX.Value;
                double dy = (p2.Y - p1.Y) * Metadata.PixelSpacingY.Value;
                return $"{Math.Sqrt(dx * dx + dy * dy):F1} mm";
            }
            return $"{Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2)):F0} px";
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            _lastPointerPos = e.GetPosition(this); _isDragging = true;
            if (ActiveTool == MouseTool.Measure) _measureStart = _lastPointerPos;
            e.Pointer.Capture(this);
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            if (!_isDragging) return;
            var pos = e.GetPosition(this);
            double dx = pos.X - _lastPointerPos.X;
            double dy = pos.Y - _lastPointerPos.Y;
            if (ActiveTool == MouseTool.Pan)
            {
                PanX += dx; PanY += dy;
                PanChanged?.Invoke(this, (PanX, PanY));
            }
            else if (ActiveTool == MouseTool.WindowLevel)
            {
                WindowCenter += dx * 10;
                WindowWidth = Math.Max(1, WindowWidth + dy * 10);
                WindowLevelChanged?.Invoke(this, (WindowCenter, WindowWidth));
            }
            _lastPointerPos = pos;
            InvalidateVisual();
        }

        protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
        {
            if (ActiveTool == MouseTool.Pan)
            {
                double delta = e.Delta.Y > 0 ? 1.1 : 0.9;
                ZoomLevel = Math.Clamp(ZoomLevel * delta, 0.1, 10.0);
                ZoomLevelChanged?.Invoke(this, ZoomLevel);
            }
            else
            {
                FrameScrolled?.Invoke(this, e.Delta.Y > 0 ? -1 : 1);
            }
            InvalidateVisual();
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            if (_isDragging && ActiveTool == MouseTool.Measure && _measureStart.HasValue)
            {
                var p1 = ScreenToImage(_measureStart.Value);
                var p2 = ScreenToImage(e.GetPosition(this));
                if (!_persistentMeasurements.ContainsKey(CurrentFrameIndex)) _persistentMeasurements[CurrentFrameIndex] = new List<MeasurementData>();
                _persistentMeasurements[CurrentFrameIndex].Add(new MeasurementData { Start = p1, End = p2, Value = CalculatePhysicalDistance(p1, p2) });
                _measureStart = null;
            }
            _isDragging = false; e.Pointer.Capture(null); InvalidateVisual();
        }

        public override void Render(DrawingContext ctx)
        {
            var bounds = Bounds; ctx.FillRectangle(Brushes.Black, new Rect(bounds.Size));
            if (_bitmap == null) return;
            double cx = bounds.Width / 2 + PanX; double cy = bounds.Height / 2 + PanY;
            double scale = Math.Min(bounds.Width / _imgWidth, bounds.Height / _imgHeight) * ZoomLevel;

            // Apply Coordinate Transforms (Flip and Rotation)
            var transform = Matrix.CreateTranslation(-cx, -cy) * Matrix.CreateScale(IsFlippedH ? -1 : 1, IsFlippedV ? -1 : 1) * Matrix.CreateRotation(Rotation * Math.PI / 180.0) * Matrix.CreateTranslation(cx, cy);

            using (ctx.PushTransform(transform)) { ctx.DrawImage(_bitmap, new Rect(cx - (_imgWidth * scale) / 2, cy - (_imgHeight * scale) / 2, _imgWidth * scale, _imgHeight * scale)); }

            // Draw measurements for current frame
            if (_persistentMeasurements.TryGetValue(CurrentFrameIndex, out var frameMeasurements))
            {
                foreach (var m in frameMeasurements)
                {
                    var s = ImageToScreen(m.Start); var e = ImageToScreen(m.End);
                    ctx.DrawLine(new Pen(Brushes.Yellow, 1.5), s, e); ctx.DrawEllipse(Brushes.Yellow, null, e, 3, 3);
                    ctx.DrawText(new FormattedText(m.Value, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Arial"), 10, Brushes.Yellow), new Point(e.X + 5, e.Y - 15));
                }
            }
            // Audit Warning HUD
            if (Metadata?.IsLossy == true) ctx.DrawText(new FormattedText("⚠️ LOSS IMAGE WARNING", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Arial", FontStyle.Normal, FontWeight.Bold), 10, Brushes.OrangeRed), new Point(10, bounds.Height - 30));
        }

        private void RebuildBitmap()
        {
            if (_pixels == null || _imgWidth <= 0 || _imgHeight <= 0) return;
            float winWidth = Math.Max(1f, (float)WindowWidth);
            float winCenter = (float)WindowCenter;
            float min = winCenter - winWidth / 2f;
            byte[] rgba = new byte[_imgWidth * _imgHeight * 4];
            for (int i = 0; i < _pixels.Length; i++)
            {
                float val = _pixels[i];
                byte v = (val <= min) ? (byte)0 : (val >= min + winWidth) ? (byte)255 : (byte)((val - min) / winWidth * 255f);
                if (IsInverted) v = (byte)(255 - v);
                int idx = i * 4; rgba[idx] = rgba[idx + 1] = rgba[idx + 2] = v; rgba[idx + 3] = 255;
            }
            _bitmap = new WriteableBitmap(new PixelSize(_imgWidth, _imgHeight), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
            using var fb = _bitmap.Lock(); Marshal.Copy(rgba, 0, fb.Address, rgba.Length);
        }
    }
}